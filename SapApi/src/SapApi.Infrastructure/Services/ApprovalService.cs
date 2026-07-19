using Microsoft.AspNetCore.Http;
using SapApi.Domain.Entities;
using SapApi.Domain.Interfaces;
using SapApi.Infrastructure.Identity;
using SapApi.Shared;
using SapApi.Shared.Enums;
using SapApi.Shared.Exceptions;
using SapApi.Shared.Requests;
using SapApi.Shared.Responses.Sap;
using System.Globalization;
using System.Text.Json;

namespace SapApi.Infrastructure.Services;

public class ApprovalService
{
    private readonly int UserId;
    private readonly string CompanyDb;
    private readonly AppDbContext context;

    public ApprovalService(AppDbContext dbContext, IHttpContextAccessor httpContext, ICurrentCompanyDbAccessor companyDbAccessor)
    {
        context = dbContext;

        var userId = httpContext.GetUserIdAsync();
        if (!userId.HasValue)
            throw new ApiErrorException(BaseErrorCodes.NullValue, "User not found!");

        UserId = userId.Value;
        CompanyDb = companyDbAccessor.GetCompanyDbName();
    }

        #region ENTRY POINT (Policy Check)

        public async Task<SapBaseResponse> CheckApprovalPolicy<TData>(
            int? policyRequestId,
            TData? data,
            ApprovalDocumentType documentType,
            ApprovalAction action, string? supportingData = null)
        {
            if (policyRequestId.HasValue)
            {
                ApprovalRequest request = await context.ApprovalRequests
                    .FirstOrDefaultAsync(x =>
                        x.Id == policyRequestId &&
                        x.CompanyDb == CompanyDb &&
                        x.Action == action)
                    ?? throw new ApiErrorException(
                        BaseErrorCodes.NullValue,
                        "Approval Request does not exist!");

                if (request.OverallStatus != ApprovalStatus.Approved)
                {
                    return new SapInventoryTransferRequestResponse
                    {
                        PendingApproval = true,
                        PendingApprovalRequestId = request.Id
                    };
                }

                return new SapInventoryTransferRequestResponse
                {
                    PendingApproval = false
                };
            }

            var requestId = await CreateRequestAsync(documentType, data!, action, supportingData);

            if (requestId != -1)
            {
                return new SapInventoryTransferRequestResponse
                {
                    PendingApproval = true,
                    PendingApprovalRequestId = requestId
                };
            }

            return new SapInventoryTransferRequestResponse
            {
                PendingApproval = false
            };
        }

        #endregion

        #region CREATE REQUEST

        private async Task<int> CreateRequestAsync<TData>(
            ApprovalDocumentType docType,
            TData data,
            ApprovalAction action, string? supportingData = null)
        {
            ApprovalPolicy? policy = await context.ApprovalPolicies
                .Include(x => x.Approvers)
                .Include(x => x.Rules)
                .FirstOrDefaultAsync(x =>
                    x.CompanyDb == CompanyDb &&
                    x.RequesterUserId == UserId &&
                    x.DocumentType == docType &&
                    x.IsActive);

            if (policy == null)
                return -1;

            // Evaluate rules
            bool requireApproval = EvaluatePolicyRules(policy, data);

            if (!requireApproval)
                return -1;

            var approvalRequest = new ApprovalRequest
            {
                CompanyDb = CompanyDb,
                DocumentType = docType,
                RequesterUserId = UserId,
                CreatedAt = DateTime.UtcNow,
                IsApproved = false,
                OverallStatus = ApprovalStatus.Pending,
                PolicyId = policy.Id,
                Action = action,
                SupportingData = supportingData,
                RequestBody = JsonSerializer.Serialize(data)
            };

            await context.ApprovalRequests.AddAsync(approvalRequest);
            await context.SaveChangesAsync();

            foreach (ApprovalPolicyApprover approver in policy.Approvers)
            {
                await context.UserApprovals.AddAsync(new UserApproval
                {
                    ApprovalRequestId = approvalRequest.Id,
                    UserId = approver.ApproverUserId,
                    Priority = approver.Priority,
                    ApprovalStatus = ApprovalStatus.Pending,
                    ActionDate = DateTime.UtcNow
                });
            }

            await AddLogAsync(approvalRequest.Id, UserId, "Created", newValue: $"{docType}/{action}");

            await context.SaveChangesAsync();

            return approvalRequest.Id;
        }

        #endregion

        #region RULE ENGINE

        private bool EvaluatePolicyRules<T>(ApprovalPolicy policy, T data)
        {
            // Rule 1: If no rules defined → approval required
            if (policy.Rules.Count == 0)
                return true;

            Type type = typeof(T);

            foreach (ApprovalPolicyRule rule in policy.Rules)
            {
                var value = ResolveRuleFieldValue(policy.DocumentType, type, data, rule.FieldName);
                if (value == null)
                    continue;

                if (!EvaluateCondition(value, rule.Operator, rule.Value))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Resolves a rule's configured field name to a value on the request object being evaluated.
        /// Mostly a direct reflection lookup, except for Payments/"DocTotal": SapVendorPaymentRequests
        /// has no DocTotal property — only TransferSum (a string, the batch's own outgoing payment
        /// total) — so the UI-facing "DocTotal" field for Payments policies is mapped onto TransferSum
        /// here instead of silently no-op'ing via a missing PropertyInfo (which would leave amount-based
        /// approval thresholds on Payments policies never actually enforced).
        /// </summary>
        private static object? ResolveRuleFieldValue<T>(ApprovalDocumentType docType, Type type, T data, string fieldName)
        {
            if (docType == ApprovalDocumentType.Payments
                && fieldName == "DocTotal"
                && data is SapVendorPaymentRequests paymentRequest)
            {
                return double.TryParse(
                    paymentRequest.TransferSum,
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out var transferSum)
                    ? transferSum
                    : null;
            }

            return type.GetProperty(fieldName)?.GetValue(data);
        }

        private bool EvaluateCondition(object actualValue,
                                       string op,
                                       string expectedValue)
        {
            try
            {
                decimal actual = Convert.ToDecimal(actualValue);
                decimal expected = Convert.ToDecimal(expectedValue);

                return op switch
                {
                    "GreaterThan" => actual > expected,
                    "GreaterThanOrEqual" => actual >= expected,
                    "LessThan" => actual < expected,
                    "LessThanOrEqual" => actual <= expected,
                    "Equal" => actual == expected,
                    _ => false
                };
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region PRE-APPROVAL VALIDATION

        public Task<ApprovalRequest?> GetRequestForActionAsync(int requestId)
        {
            return context.ApprovalRequests
                .Include(x => x.UserApprovals)
                .FirstOrDefaultAsync(x => x.Id == requestId && x.CompanyDb == CompanyDb);
        }

        /// <summary>
        /// Determines, without mutating state, whether the given user approving now would push the
        /// request to OverallStatus.Approved (i.e. this is the final outstanding level). Used to enforce
        /// pre-conditions (e.g. UTR details for payments) before ApproveAsync commits the approval, since
        /// ApproveAsync cannot be safely retried once a user's approval has been recorded.
        /// </summary>
        public static bool WouldCompleteApproval(ApprovalRequest request, int userId)
        {
            UserApproval? currentApproval = request.UserApprovals.FirstOrDefault(x => x.UserId == userId);
            if (currentApproval == null || currentApproval.ApprovalStatus != ApprovalStatus.Pending)
                return false;

            if (request.UserApprovals.Any(x => x.ApprovalStatus == ApprovalStatus.Rejected))
                return false;

            return request.UserApprovals
                .Where(x => x.Priority != currentApproval.Priority)
                .GroupBy(x => x.Priority)
                .All(g => g.Any(x => x.ApprovalStatus == ApprovalStatus.Approved));
        }

        #endregion

        #region APPROVE

        public async Task<ApprovalRequest?> ApproveAsync(
            int requestId,
            int userId,
            string comment,
            string? body = null)
        {
            UserApproval userApproval = await context.UserApprovals
                .FirstOrDefaultAsync(x =>
                    x.ApprovalRequestId == requestId &&
                    x.UserId == userId)
                ?? throw new ApiErrorException(
                    BaseErrorCodes.Forbidden,
                    "You are not an approver for this request.");

            if (userApproval.ApprovalStatus != ApprovalStatus.Pending)
                throw new ApiErrorException(BaseErrorCodes.Conflict, "This request has already been processed.");

            userApproval.ApprovalStatus = ApprovalStatus.Approved;
            userApproval.Comment = comment;
            userApproval.ActionDate = DateTime.UtcNow;
            // DbContext is configured with QueryTrackingBehavior.NoTracking globally (see
            // DependencyInjection.cs) for scalability — entities fetched via queries are NOT
            // change-tracked, so mutations above are silently dropped unless we explicitly mark them
            // Modified before SaveChangesAsync. We use Entry(...).State rather than DbSet.Update(...)
            // because Update() walks the whole navigation graph and re-attaches it too, which throws a
            // duplicate-key tracking conflict once EvaluateRequestStatus below re-queries the same
            // UserApproval row (via its Include) inside this same DbContext/request.
            context.Entry(userApproval).State = EntityState.Modified;

            if (!string.IsNullOrEmpty(body))
            {
                ApprovalRequest request = await context.ApprovalRequests
                    .FirstAsync(x => x.Id == requestId);

                request.RequestBody = body;
                context.Entry(request).State = EntityState.Modified;
            }

            await AddLogAsync(requestId, userId, "Approved", comment: comment);
            await context.SaveChangesAsync();

            return await EvaluateRequestStatus(requestId);
        }

        #endregion

        #region REJECT

        public async Task RejectAsync(
            int requestId,
            int userId,
            string comment)
        {
            UserApproval userApproval = await context.UserApprovals
                .FirstOrDefaultAsync(x =>
                    x.ApprovalRequestId == requestId &&
                    x.UserId == userId)
                ?? throw new ApiErrorException(
                    BaseErrorCodes.Forbidden,
                    "You are not an approver for this request.");

            if (userApproval.ApprovalStatus != ApprovalStatus.Pending)
                throw new ApiErrorException(BaseErrorCodes.Conflict, "This request has already been processed.");

            userApproval.ApprovalStatus = ApprovalStatus.Rejected;
            userApproval.Comment = comment;
            userApproval.ActionDate = DateTime.UtcNow;
            context.Entry(userApproval).State = EntityState.Modified;

            await AddLogAsync(requestId, userId, "Rejected", comment: comment);
            await context.SaveChangesAsync();

            await EvaluateRequestStatus(requestId);
        }

        public async Task FailedAsync(int requestId, string comment)
        {
            var request = await context.ApprovalRequests
                .FirstOrDefaultAsync(x => x.Id == requestId);
            if (request == null)
                throw new ApiErrorException(BaseErrorCodes.NullValue, "Request not found!");
            request.OverallStatus = ApprovalStatus.Failed;
            request.FailureReason = comment;

            context.ApprovalRequests.Entry(request).State = EntityState.Modified;
            await AddLogAsync(requestId, UserId, "Failed", comment: comment);
            await context.SaveChangesAsync();
        }

        #endregion

        #region AUDIT LOG

        private async Task AddLogAsync(int approvalRequestId, int actionByUserId, string action, string? comment = null, string? oldValue = null, string? newValue = null)
        {
            await context.ApprovalLogs.AddAsync(new ApprovalLog
            {
                CompanyDb = CompanyDb,
                ApprovalRequestId = approvalRequestId,
                ActionByUserId = actionByUserId,
                Action = action,
                Comment = comment,
                OldValue = oldValue,
                NewValue = newValue,
                CreatedAt = DateTime.UtcNow
            });
        }

        #endregion

        #region STAGE EVALUATION

        private async Task<ApprovalRequest?> EvaluateRequestStatus(int requestId)
        {
            ApprovalRequest request = await context.ApprovalRequests
                .Include(r => r.UserApprovals)
                .FirstAsync(r => r.Id == requestId);

            // Entry(...).State rather than Update(): the entity was fetched with Include(UserApprovals),
            // and Update() would also try to re-attach that collection, colliding with the UserApproval
            // row ApproveAsync/RejectAsync already marked Modified earlier in this same DbContext scope.
            // Entry(...).State only marks the ApprovalRequest root itself.

            // Rejection override
            if (request.UserApprovals
                .Any(x => x.ApprovalStatus == ApprovalStatus.Rejected))
            {
                request.OverallStatus = ApprovalStatus.Rejected;
                request.IsApproved = false;
                context.Entry(request).State = EntityState.Modified;
                await context.SaveChangesAsync();
                return request;
            }

            IOrderedEnumerable<IGrouping<int, UserApproval>> groupedByPriority = request.UserApprovals
                .GroupBy(x => x.Priority)
                .OrderBy(g => g.Key);

            foreach (IGrouping<int, UserApproval>? level in groupedByPriority)
            {
                bool levelApproved =
                    level.Any(x => x.ApprovalStatus == ApprovalStatus.Approved);

                if (!levelApproved)
                {
                    request.OverallStatus = ApprovalStatus.Forwarded;
                    request.IsApproved = false;
                    context.Entry(request).State = EntityState.Modified;
                    await context.SaveChangesAsync();
                    return request;
                }
            }

            request.OverallStatus = ApprovalStatus.Approved;
            request.IsApproved = true;
            context.Entry(request).State = EntityState.Modified;
            await context.SaveChangesAsync();
            return request;
        }

        #endregion

        #region VISIBILITY CONTROL

        public async Task<List<ApprovalRequest>> GetPendingForUserAsync(int userId)
        {
            // Filter at the database: only requests where this user currently has an outstanding
            // approval, instead of loading every approval request for the company into memory.
            List<ApprovalRequest> requests = await context.ApprovalRequests
                .Include(r => r.UserApprovals)
                .Include(x => x.Policy)
                .Include(x => x.RequesterUser)
                .Where(r => r.CompanyDb == CompanyDb
                    && (r.OverallStatus == ApprovalStatus.Pending || r.OverallStatus == ApprovalStatus.Forwarded)
                    && r.UserApprovals.Any(u => u.UserId == userId && u.ApprovalStatus == ApprovalStatus.Pending))
                .ToListAsync();

            var visibleRequests = new List<ApprovalRequest>();

            foreach (ApprovalRequest request in requests)
            {
                UserApproval userApproval = request.UserApprovals.First(x => x.UserId == userId);

                var currentPriority = userApproval.Priority;
                var maxPriority = request.UserApprovals.Max(x => x.Priority);
                request.IsLastApproval = currentPriority == maxPriority;
                var previousLevelsApproved = request.UserApprovals
                    .Where(x => x.Priority < currentPriority)
                    .GroupBy(x => x.Priority)
                    .All(g => g.Any(x =>
                        x.ApprovalStatus == ApprovalStatus.Approved));

                if (previousLevelsApproved)
                    visibleRequests.Add(request);
            }

            return visibleRequests;
        }

        #endregion

        #region HISTORY

        public Task<List<ApprovalRequest>> GetRequestsByRequesterAsync(int userId)
        {
            return context.ApprovalRequests
                 .Include(x => x.UserApprovals)
                 .Include(x => x.RequesterUser)
                .Include(x => x.Policy)
                 .Where(x => x.CompanyDb == CompanyDb && x.RequesterUserId == userId)
                 .OrderByDescending(x => x.CreatedAt)
                 .ToListAsync();
        }

        #endregion
}