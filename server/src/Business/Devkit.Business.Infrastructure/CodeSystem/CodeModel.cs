using Devkit.Business.Domain.CodeSystem;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Devkit.Business.Infrastructure.CodeSystem;

internal static class CodeModel
{
    internal static void Configure(ModelBuilder model, bool sqlServer)
    {
        var rule = model.Entity<CodeRule>();
        var jsonCheck = sqlServer ? "ISJSON" : "json_valid";
        rule.ToTable("sys_code_rule", table =>
        {
            table.HasCheckConstraint("ck_sys_code_rule_status",
                "(status = 'unpublished' AND active_version_no IS NULL) OR (status IN ('enabled','disabled') AND active_version_no IS NOT NULL)");
            table.HasCheckConstraint("ck_sys_code_rule_draft", $"draft_json IS NULL OR {jsonCheck}(draft_json) = 1");
            table.HasCheckConstraint("ck_sys_code_rule_revision", "revision >= 1");
        });
        rule.HasKey(x => x.Id);
        KeyText(rule.Property(x => x.RuleKey), 64);
        KeyText(rule.Property(x => x.ModuleKey), 64);
        rule.Property(x => x.Name).HasMaxLength(100);
        rule.Property(x => x.Status).IsUnicode(false).HasMaxLength(16);
        rule.Property(x => x.Revision).IsConcurrencyToken();
        rule.HasIndex(x => x.RuleKey).IsUnique();
        rule.HasIndex(x => new { x.ModuleKey, x.Status, x.UpdatedAtUtc, x.Id });

        var version = model.Entity<CodeVersion>();
        version.ToTable("sys_code_version", table =>
        {
            table.HasCheckConstraint("ck_sys_code_version_definition", $"{jsonCheck}(definition_json) = 1");
            table.HasCheckConstraint("ck_sys_code_version_number", "version_no >= 1");
        });
        version.HasKey(x => new { x.RuleId, x.VersionNo });
        version.Property(x => x.Reason).HasMaxLength(256);
        version.HasOne<CodeRule>().WithMany().HasForeignKey(x => x.RuleId).OnDelete(DeleteBehavior.Restrict);
        rule.HasOne<CodeVersion>().WithMany().HasForeignKey(x => new { x.Id, x.ActiveVersionNo }).OnDelete(DeleteBehavior.Restrict);

        var counter = model.Entity<CodeCounter>();
        counter.ToTable("sys_code_counter", table => table.HasCheckConstraint("ck_sys_code_counter_value", "last_value BETWEEN 1 AND 999999999999"));
        counter.HasKey(x => new { x.RuleId, x.PeriodKey });
        KeyText(counter.Property(x => x.PeriodKey), 8);
        counter.HasOne<CodeRule>().WithMany().HasForeignKey(x => x.RuleId).OnDelete(DeleteBehavior.Restrict);

        var issue = model.Entity<CodeIssue>();
        issue.ToTable("sys_code_issue", table => table.HasCheckConstraint("ck_sys_code_issue_value", "sequence_value BETWEEN 1 AND 999999999999"));
        issue.HasKey(x => x.Id);
        KeyText(issue.Property(x => x.PeriodKey), 8);
        KeyText(issue.Property(x => x.Code), 64);
        issue.HasOne<CodeVersion>().WithMany().HasForeignKey(x => new { x.RuleId, x.VersionNo }).OnDelete(DeleteBehavior.Restrict);
        issue.HasOne<CodeCounter>().WithMany().HasForeignKey(x => new { x.RuleId, x.PeriodKey }).OnDelete(DeleteBehavior.Restrict);
        issue.HasIndex(x => new { x.RuleId, x.PeriodKey, x.SequenceValue }).IsUnique();
        issue.HasIndex(x => new { x.RuleId, x.RequestId }).IsUnique();
        issue.HasIndex(x => new { x.RuleId, x.BusinessId }).IsUnique();
        issue.HasIndex(x => new { x.RuleId, x.Code }).IsUnique();
        issue.HasIndex(x => new { x.RuleId, x.IssuedAtUtc, x.Id });

        var change = model.Entity<CodeEvent>();
        change.ToTable("sys_code_event", table => table.HasCheckConstraint("ck_sys_code_event_action",
            "action IN ('create','save_draft','publish','disable','enable')"));
        change.HasKey(x => x.Id);
        change.Property(x => x.Action).IsUnicode(false).HasMaxLength(16);
        change.Property(x => x.Reason).HasMaxLength(256);
        change.HasOne<CodeRule>().WithMany().HasForeignKey(x => x.RuleId).OnDelete(DeleteBehavior.Restrict);
        change.HasOne<CodeVersion>().WithMany().HasForeignKey(x => new { x.RuleId, x.VersionNo }).OnDelete(DeleteBehavior.Restrict);
        change.HasIndex(x => new { x.RuleId, x.Revision }).IsUnique();
        change.HasIndex(x => new { x.RuleId, x.OccurredAtUtc, x.Id });

        foreach (var entity in new[] { rule.Metadata, version.Metadata, counter.Metadata, issue.Metadata, change.Metadata })
            foreach (var property in entity.GetProperties().Where(p => p.ClrType == typeof(DateTime)))
                model.Entity(entity.ClrType).Property<DateTime>(property.Name)
                    .HasConversion(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

        void KeyText(PropertyBuilder<string> column, int size)
        {
            column.HasMaxLength(size).IsUnicode(false);
            if (sqlServer) column.UseCollation("Latin1_General_100_BIN2");
        }
    }
}
