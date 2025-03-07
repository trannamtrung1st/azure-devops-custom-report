CREATE
OR REPLACE VIEW "UsSummary" AS
SELECT
    row_number() OVER () AS "No",
    wi."Fields" ->> 'System.AreaLevel2' AS "Team",
    wi."Fields" ->> 'System.IterationLevel2' AS "Sprint",
    wi."Fields" ->> 'System.Id' AS "ID",
    concat(
        'https://dev.azure.com/AssetHealthInsights/Asset%20Backlogs/_workitems/edit/',
        wi."Fields" ->> 'System.Id'
    ) AS "US Link",
    wi."Fields" ->> 'System.Title' AS "Title",
    wi."Fields" ->> 'Microsoft.VSTS.Scheduling.StoryPoints' AS "Story Points",
    wi."Fields" -> 'Custom.FEby' ->> 'UniqueName' AS "FE by",
    wi."Fields" -> 'Custom.BEby' ->> 'UniqueName' AS "BE by",
    wi."Fields" ->> 'System.State' AS "US Status",
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
WHERE
    wi."Type" = 'User Story';