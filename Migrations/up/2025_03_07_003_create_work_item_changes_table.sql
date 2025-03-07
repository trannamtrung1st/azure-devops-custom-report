CREATE TABLE IF NOT EXISTS "WorkItemChanges" (
    "Id" SERIAL PRIMARY KEY,
    "WorkItemId" integer NOT NULL,
    "ChangedDate" timestamp with time zone NOT NULL,
    "ChangedBy" character varying(256) NOT NULL,
    "BeforeFields" jsonb,
    "AfterFields" jsonb,
    CONSTRAINT "FK_WorkItemChanges_WorkItems_WorkItemId" 
        FOREIGN KEY ("WorkItemId") 
        REFERENCES "WorkItems" ("Id") 
        ON DELETE CASCADE
);

CREATE INDEX "IX_WorkItemChanges_WorkItemId" ON "WorkItemChanges"("WorkItemId");
CREATE INDEX "IX_WorkItemChanges_ChangedDate" ON "WorkItemChanges"("ChangedDate");
CREATE INDEX "IX_WorkItemChanges_ChangedBy" ON "WorkItemChanges"("ChangedBy");
CREATE INDEX "IX_WorkItemChanges_BeforeFields" ON "WorkItemChanges" USING gin ("BeforeFields");
CREATE INDEX "IX_WorkItemChanges_AfterFields" ON "WorkItemChanges" USING gin ("AfterFields");