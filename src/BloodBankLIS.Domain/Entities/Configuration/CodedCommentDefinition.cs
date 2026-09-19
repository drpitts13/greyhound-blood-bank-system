using BloodBankLIS.Domain.Common;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Domain.Entities.Configuration;

/// <summary>
/// Admin catalog of coded comments. Selecting a code on the matching chart page
/// inserts <see cref="CommentText"/> into the free-text comment field.
/// </summary>
public class CodedCommentDefinition : BaseEntity
{
    public string Code { get; set; } = string.Empty;

    public string CommentText { get; set; } = string.Empty;

    public CommentPage Page { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
}
