CREATE TABLE IF NOT EXISTS "WorkItems" (
    "Id" integer NOT NULL,
    "Type" character varying(100) NOT NULL,
    "State" character varying(100) NOT NULL,
    "Title" character varying(500) NOT NULL,
    "AreaPath" character varying(500),
    "ParentId" integer,
    "CreatedDate" timestamp with time zone NOT NULL,
    "ChangedDate" timestamp with time zone,
    "Fields" jsonb NOT NULL,
    CONSTRAINT "PK_WorkItems" PRIMARY KEY ("Id")
);

CREATE INDEX "IX_WorkItems_Type" ON "WorkItems"("Type");
CREATE INDEX "IX_WorkItems_State" ON "WorkItems"("State");
CREATE INDEX "IX_WorkItems_AreaPath" ON "WorkItems"("AreaPath");
CREATE INDEX "IX_WorkItems_ParentId" ON "WorkItems"("ParentId");
CREATE INDEX "IX_WorkItems_CreatedDate" ON "WorkItems"("CreatedDate");
CREATE INDEX "IX_WorkItems_ChangedDate" ON "WorkItems"("ChangedDate");