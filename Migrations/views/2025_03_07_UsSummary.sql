DROP VIEW IF EXISTS "UsSummary";

CREATE VIEW "UsSummary" AS
SELECT
    row_number() OVER () AS "No",
    wi."Fields" ->> 'System.AreaLevel2' AS "Team",
    wi."Fields" ->> 'System.IterationLevel2' AS "Sprint",
    wi."Fields" ->> 'System.Id' AS "ID",
    concat(
        'https://dev.azure.com/AssetHealthInsights/Asset%20Backlogs/_workitems/edit/',
        wi."Fields" ->> 'System.Id'
    ) AS "US Link",
    parent."Title" AS "Feature",
    wi."Fields" ->> 'System.Title' AS "Title",
    wi."Fields" ->> 'Microsoft.VSTS.Scheduling.StoryPoints' AS "Story Points",
    usi."Sprint Cycle",
    usi."Sprint Path",
    SPLIT_PART(
        wi."Fields" -> 'Custom.FEby' ->> 'UniqueName',
        '@',
        1
    ) AS "FE Engineer",
    SPLIT_PART(
        wi."Fields" -> 'Custom.BEby' ->> 'UniqueName',
        '@',
        1
    ) AS "BE Engineer",
    CASE
        WHEN wi."Fields" ->> 'System.State' = 'New' THEN 'New'
        WHEN wi."Fields" ->> 'System.State' = 'Active' THEN 'Active'
        WHEN wi."Fields" ->> 'System.State' = 'Resolved' THEN 'Active'
        WHEN wi."Fields" ->> 'System.State' = 'Testing' THEN 'Active'
        WHEN wi."Fields" ->> 'System.State' = 'Re-open' THEN 'Active'
        WHEN wi."Fields" ->> 'System.State' = 'On-Hold' THEN 'On-Hold'
        WHEN wi."Fields" ->> 'System.State' = 'Removed' THEN 'Removed'
        ELSE 'Closed'
    END AS "US Status",
    wi."Fields" ->> 'System.State' AS "State",
    COALESCE(up."Current Progress", 0) AS "Current Progress",
    wi."Fields" ->> 'Microsoft.VSTS.Common.Priority' AS "Priority",
    COALESCE(up."Actual Effort", 0) AS "Actual Effort",
    wi."Fields" ->> 'Custom.TotalDevEffort' AS "Estimated Dev Effort",
    wi."Fields" ->> 'Custom.TotalQCEffort' AS "Estimated QC Effort",
    wi."Fields" ->> 'Custom.TotalBAEffort' AS "Estimated BA Effort",
    wi."Fields" ->> 'Custom.TotalDevOpsEffort' AS "Estimated DevOps Effort",
    (
        COALESCE(
            (wi."Fields" ->> 'Custom.TotalDevEffort') :: float,
            0
        ) + COALESCE(
            (wi."Fields" ->> 'Custom.TotalQCEffort') :: float,
            0
        ) + COALESCE(
            (wi."Fields" ->> 'Custom.TotalBAEffort') :: float,
            0
        ) + COALESCE(
            (wi."Fields" ->> 'Custom.TotalDevOpsEffort') :: float,
            0
        )
    ) AS "Estimated Total Effort",
    (wi."Fields" ->> 'Custom.DEVStartDate') :: timestamp AS "Actual Start Date",
    (wi."Fields" ->> 'Custom.USDueDate') :: timestamp AS "Expected UAT Date",
    (wi."Fields" ->> 'Custom.ActualUATDate') :: timestamp AS "Actual UAT Date",
    (wi."Fields" ->> 'Custom.ActualUATDate') :: timestamp <= (wi."Fields" ->> 'Custom.USDueDate') :: timestamp AS "Delivery On Time"
FROM
    "WorkItems" wi
    LEFT JOIN (
        SELECT
            wi."Id",
            wi."Title"
        FROM
            "WorkItems" wi
        WHERE
            wi."Type" IN('Feature', 'Epic')
    ) parent ON wi."ParentId" = parent."Id"
    AND wi."Type" = 'User Story'
    LEFT JOIN "UsProgress" up ON wi."Id" = up."Id"
    LEFT JOIN "UsSprintsInfo" usi ON wi."Id" = usi."Id"
WHERE
    wi."Type" = 'User Story';