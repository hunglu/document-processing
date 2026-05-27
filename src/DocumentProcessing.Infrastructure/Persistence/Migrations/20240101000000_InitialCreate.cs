using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocumentProcessing.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Documents",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                FileName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                BlobPath = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                Checksum = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                PageCount = table.Column<int>(type: "int", nullable: true),
                Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                FailureReason = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                FailureMessage = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Documents", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "DocumentPages",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PageNumber = table.Column<int>(type: "int", nullable: false),
                ExtractedText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DocumentPages", x => x.Id);
                table.ForeignKey(
                    name: "FK_DocumentPages_Documents_DocumentId",
                    column: x => x.DocumentId,
                    principalTable: "Documents",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "AuditEntries",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FromStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                ToStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                Message = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                Actor = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AuditEntries", x => x.Id);
                table.ForeignKey(
                    name: "FK_AuditEntries_Documents_DocumentId",
                    column: x => x.DocumentId,
                    principalTable: "Documents",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Documents_TenantId",
            table: "Documents",
            column: "TenantId");

        migrationBuilder.CreateIndex(
            name: "IX_Documents_TenantId_Status",
            table: "Documents",
            columns: new[] { "TenantId", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_Documents_CreatedAt",
            table: "Documents",
            column: "CreatedAt");

        migrationBuilder.CreateIndex(
            name: "IX_DocumentPages_DocumentId_PageNumber",
            table: "DocumentPages",
            columns: new[] { "DocumentId", "PageNumber" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_AuditEntries_DocumentId",
            table: "AuditEntries",
            column: "DocumentId");

        migrationBuilder.CreateIndex(
            name: "IX_AuditEntries_OccurredAt",
            table: "AuditEntries",
            column: "OccurredAt");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "AuditEntries");
        migrationBuilder.DropTable(name: "DocumentPages");
        migrationBuilder.DropTable(name: "Documents");
    }
}
