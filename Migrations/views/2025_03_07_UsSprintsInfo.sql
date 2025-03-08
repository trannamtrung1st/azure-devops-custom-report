CREATE
OR REPLACE VIEW "UsSprintsInfo" AS WITH sc AS (
    SELECT
        wic."WorkItemId" "Id",
        COUNT(wic."WorkItemId") + 1 "Sprint Cycle"
    FROM
        "WorkItemChanges" wic
    WHERE
        (
            wic."BeforeFields" ->> 'System.IterationLevel2' IS NOT NULL
            AND wic."AfterFields" ->> 'System.IterationLevel2' IS NOT NULL
        )
    GROUP BY
        wic."WorkItemId"
    ORDER BY
        wic."WorkItemId"
),
sp AS (
    SELECT
        wic."WorkItemId" "Id",
        array_to_string(
            ARRAY_AGG(
                wic."AfterFields" ->> 'System.IterationLevel2'
                ORDER BY
                    wic."ChangedDate"
            ),
            ' / '
        ) as "Sprint Path"
    FROM
        "WorkItemChanges" wic
    WHERE
        wic."AfterFields" ->> 'System.IterationLevel2' IS NOT NULL
    GROUP BY
        wic."WorkItemId"
)
SELECT
    wi."Id",
    coalesce(sc."Sprint Cycle", 1) "Sprint Cycle",
    sp."Sprint Path"
FROM
    "WorkItems" wi
    LEFT JOIN sc ON wi."Id" = sc."Id"
    AND wi."Type" = 'User Story'
    LEFT JOIN sp ON wi."Id" = sp."Id"
    AND wi."Type" = 'User Story'
WHERE
    wi."Type" = 'User Story';