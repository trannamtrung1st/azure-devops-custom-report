DROP VIEW IF EXISTS "UsProgress";

CREATE VIEW "UsProgress" AS WITH UsEfforts AS (
    SELECT
        wi."ParentId" as "Id",
        SUM(
            COALESCE(
                (
                    wi."Fields" ->> 'Microsoft.VSTS.Scheduling.RemainingWork'
                ) :: float,
                0
            )
        ) AS "Remaining Work",
        SUM(
            COALESCE(
                (
                    wi."Fields" ->> 'Microsoft.VSTS.Scheduling.CompletedWork'
                ) :: float,
                0
            )
        ) AS "Completed Work"
    FROM
        "WorkItems" wi
        INNER JOIN "WorkItems" parent ON wi."ParentId" = parent."Id"
        AND parent."State" NOT IN ('Removed', 'Closed')
    WHERE
        wi."Type" = 'Task'
        OR wi."Type" = 'Bug'
    GROUP BY
        wi."ParentId"
)
SELECT
    wi."Id",
    wi."Remaining Work",
    wi."Completed Work",
    wi."Remaining Work" + wi."Completed Work" AS "Actual Effort",
    CASE
        WHEN wi."Remaining Work" + wi."Completed Work" = 0 THEN 100
        ELSE ROUND(
            (
                wi."Completed Work" / (wi."Remaining Work" + wi."Completed Work")
            ) * 100
        )
    END AS "Current Progress"
FROM
    UsEfforts wi