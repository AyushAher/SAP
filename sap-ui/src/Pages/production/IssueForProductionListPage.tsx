import { useCallback, useMemo } from "react";
import { FileDown, Pencil } from "lucide-react";
import { PageHeader } from "@/Components/shared/PageHeader";
import { RowActionButton, RowActionLink, RowActions, rowActionIconClassName } from "@/Components/shared/RowActions";
import { DataTable, type DataTableColumn } from "@/Components/ui";
import { ROUTES } from "@/config/constants";
import { formatCodeWithName } from "@/helpers/masterLookup";
import { useEnrichedListFetch } from "@/hooks/useEnrichedListFetch";
import {
  downloadIssueForProductionPdf,
  listIssueForProduction,
  type IssueForProductionRequest,
} from "@/Requests/issueForProduction";

const extractors = {
  itemCodes: (row: IssueForProductionRequest) => row.itemNo,
  projectCodes: (row: IssueForProductionRequest) => row.project,
  cardCodes: (row: IssueForProductionRequest) => row.cardCode,
};

export function IssueForProductionListPage() {
  const fetchRequests = useCallback(
    (request: Parameters<typeof listIssueForProduction>[0]) =>
      listIssueForProduction(request),
    [],
  );
  const { fetchData, lookupMaps } = useEnrichedListFetch(
    fetchRequests,
    extractors,
  );

  const columns = useMemo<DataTableColumn<IssueForProductionRequest>[]>(
    () => [
      {
        key: "id",
        header: "Request #",
        sortable: true,
        filterable: true,
        accessor: (r) => r.id,
      },
      {
        key: "cardCode",
        header: "Customer",
        sortable: true,
        filterable: true,
        accessor: (r) =>
          formatCodeWithName(
            r.cardCode,
            r.cardName ?? lookupMaps.businessPartners[r.cardCode],
          ),
      },
      {
        key: "project",
        header: "Project",
        sortable: true,
        filterable: true,
        accessor: (r) =>
          formatCodeWithName(
            r.project,
            r.projectName ?? lookupMaps.projects[r.project],
          ),
      },
      {
        key: "itemNo",
        header: "Item",
        sortable: true,
        filterable: true,
        accessor: (r) =>
          formatCodeWithName(
            r.itemNo,
            r.itemName ?? lookupMaps.items[r.itemNo],
          ),
      },
      {
        key: "status",
        header: "Status",
        sortable: true,
        filterable: true,
        accessor: (r) => r.status,
      },
      {
        key: "actions",
        header: "Actions",
        render: (row) => {
          if (!row.id) return null;
          const id = row.id;
          return (
            <RowActions>
              <RowActionLink
                to={`${ROUTES.ISSUE_FOR_PRODUCTION_FORM}/${id}`}
                title="Edit issue request"
                icon={<Pencil className={rowActionIconClassName} />}
              />
              <RowActionButton
                title="Download PDF"
                icon={<FileDown className={rowActionIconClassName} />}
                onClick={() => downloadIssueForProductionPdf(id)}
              />
            </RowActions>
          );
        },
      },
    ],
    [lookupMaps],
  );

  return (
    <div className="space-y-6">
      <PageHeader
        title="Issue For Production"
        actionLabel="Add New"
        actionTo={ROUTES.ISSUE_FOR_PRODUCTION_FORM}
      />
      <DataTable
        columns={columns}
        fetchData={fetchData}
        getRowKey={(r) => r.id ?? Math.random()}
        initialSorts={[{ field: "id", direction: "desc" }]}
      />
    </div>
  );
}
