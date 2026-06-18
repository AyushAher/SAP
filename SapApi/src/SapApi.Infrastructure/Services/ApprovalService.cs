using Microsoft.AspNetCore.Http;
using SapApi.Domain.Entities;
using SapApi.Infrastructure.Identity;
using SapApi.Shared;
using SapApi.Shared.Enums;
using SapApi.Shared.Exceptions;
using SapApi.Shared.Responses.Sap;
using System.Reflection;
using System.Text.Json;

namespace SapApi.Infrastructure.Services;

public class ApprovalService
{
    private readonly int UserId;
    private readonly AppDbContext context;

    public ApprovalService(AppDbContext dbContext, IHttpContextAccessor httpContext)
    {
        context = dbContext;

        var userId = httpContext.GetUserIdAsync();
            if (!userId.HasValue)
                throw new ApiErrorException(BaseErrorCodes.NullValue, "User not found!");

            UserId = userId.Value;
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
                PropertyInfo? property = type.GetProperty(rule.FieldName);
                if (property == null)
                    continue;

                var value = property.GetValue(data);
                if (value == null)
                    continue;

                if (!EvaluateCondition(value, rule.Operator, rule.Value))
                    return false;
            }

            return true;
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

        #region APPROVE

        public async Task<ApprovalRequest?> ApproveAsync(
            int requestId,
            int userId,
            string comment,
            string? body = null)
        {
            UserApproval userApproval = await context.UserApprovals
                .FirstAsync(x =>
                    x.ApprovalRequestId == requestId &&
                    x.UserId == userId);

            if (userApproval.ApprovalStatus != ApprovalStatus.Pending)
                throw new Exception("Already processed.");

            userApproval.ApprovalStatus = ApprovalStatus.Approved;
            userApproval.Comment = comment;
            userApproval.ActionDate = DateTime.UtcNow;

            if (!string.IsNullOrEmpty(body))
            {
                ApprovalRequest request = await context.ApprovalRequests
                    .FirstAsync(x => x.Id == requestId);

                request.RequestBody = body;
            }

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
                .FirstAsync(x =>
                    x.ApprovalRequestId == requestId &&
                    x.UserId == userId);

            userApproval.ApprovalStatus = ApprovalStatus.Rejected;
            userApproval.Comment = comment;
            userApproval.ActionDate = DateTime.UtcNow;

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
            await context.SaveChangesAsync();
        }

        #endregion

        #region STAGE EVALUATION

        private async Task<ApprovalRequest?> EvaluateRequestStatus(int requestId)
        {
            ApprovalRequest request = await context.ApprovalRequests
                .Include(r => r.UserApprovals)
                .FirstAsync(r => r.Id == requestId);

            // Rejection override
            if (request.UserApprovals
                .Any(x => x.ApprovalStatus == ApprovalStatus.Rejected))
            {
                request.OverallStatus = ApprovalStatus.Rejected;
                request.IsApproved = false;
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
                    await context.SaveChangesAsync();
                    return request;
                }
            }

            request.OverallStatus = ApprovalStatus.Approved;
            request.IsApproved = true;
            await context.SaveChangesAsync();
            return request;
        }

        #endregion

        #region VISIBILITY CONTROL

        public async Task<List<ApprovalRequest>> GetPendingForUserAsync(int userId)
        {
            List<ApprovalRequest> requests = await context.ApprovalRequests
                .Include(r => r.UserApprovals)
                .Include(x => x.Policy)
                .Include(x => x.RequesterUser)
                .ToListAsync();

            var visibleRequests = new List<ApprovalRequest>();

            foreach (ApprovalRequest? request in requests)
            {
                UserApproval? userApproval = request.UserApprovals
                    .FirstOrDefault(x => x.UserId == userId);

                if (userApproval == null)
                    continue;

                if (userApproval.ApprovalStatus != ApprovalStatus.Pending)
                    continue;

                if (request.OverallStatus is not ApprovalStatus.Pending and not ApprovalStatus.Forwarded)
                    continue;

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
                 .Where(x => x.RequesterUserId == userId)
                 .OrderByDescending(x => x.CreatedAt)
                 .ToListAsync();
        }

        #endregion
}