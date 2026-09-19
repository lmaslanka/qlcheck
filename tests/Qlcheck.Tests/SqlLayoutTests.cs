namespace Qlcheck.Tests;

public class SqlLayoutTests
{
    [Fact]
    public void Formats_select_with_river_alignment_trailing_commas_and_uppercase_keywords()
    {
        var sql = """
            SELECT c.record_id
                                  ,c.name
                                  ,c.code_alpha2
                              FROM countries As c
                               WHERE c.is_deleted = false
                              ORDER BY c.name ASC;
            """;

        var formatted = SqlLayout.Format(sql);

        Assert.Equal(
            """
              SELECT c.record_id,
                     c.name,
                     c.code_alpha2
                FROM countries AS c
               WHERE c.is_deleted = FALSE
            ORDER BY c.name ASC;
            """.ReplaceLineEndings("\n"),
            formatted);
    }

    [Fact]
    public void Formats_insert_column_list_one_per_line()
    {
        var sql = """
            INSERT INTO activity_feed (
                audit_event_id, occurred_on, actor_kind, actor_user_id, actor_auth0_id, actor_display_name, subject_type, subject_record_id, subject_label, parent_subject_type, parent_subject_record_id, action
            )
            """;

        var formatted = SqlLayout.Format(sql);

        Assert.Equal(
            """
            INSERT INTO activity_feed (
                audit_event_id,
                occurred_on,
                actor_kind,
                actor_user_id,
                actor_auth0_id,
                actor_display_name,
                subject_type,
                subject_record_id,
                subject_label,
                parent_subject_type,
                parent_subject_record_id,
                action
            )
            """.ReplaceLineEndings("\n"),
            formatted);
    }

    [Fact]
    public void Puts_where_and_on_its_own_line()
    {
        var sql = """
            SELECT c.record_id
              FROM countries AS c
             WHERE c.is_deleted = false AND c.name = 'x'
             ORDER BY c.name ASC;
            """;

        var formatted = SqlLayout.Format(sql);

        Assert.Equal(
            """
              SELECT c.record_id
                FROM countries AS c
               WHERE c.is_deleted = FALSE
                 AND c.name = 'x'
            ORDER BY c.name ASC;
            """.ReplaceLineEndings("\n"),
            formatted);
    }

    [Fact]
    public void Rewrites_count_star_to_count_one()
    {
        var formatted = SqlLayout.Format("SELECT COUNT(*) FROM t");

        Assert.Equal(
            """
            SELECT COUNT(1)
              FROM t
            """.ReplaceLineEndings("\n"),
            formatted);
    }

    [Fact]
    public void Formats_update_set_one_assignment_per_line()
    {
        var sql = """
            UPDATE users AS u
            SET page_size = @pageSize, modified_by = @userId, modified_on = CURRENT_TIMESTAMP
            WHERE u.user_id = @userId AND u.is_deleted = false;
            """;

        var formatted = SqlLayout.Format(sql);

        Assert.Equal(
            """
            UPDATE users AS u
               SET page_size = @pageSize,
                   modified_by = @userId,
                   modified_on = CURRENT_TIMESTAMP
             WHERE u.user_id = @userId
               AND u.is_deleted = FALSE;
            """.ReplaceLineEndings("\n"),
            formatted);
    }
}
