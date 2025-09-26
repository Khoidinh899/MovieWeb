using System;
using System.Collections.Generic;

namespace MovieWeb.Models.Entities;

public partial class Comment
{
    public int CommentId { get; set; }

    public int UserId { get; set; }

    public int MovieId { get; set; }

    public int? ParentCommentId { get; set; }

    public string Content { get; set; } = null!;

    public bool? IsActive { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<Comment> InverseParentComment { get; set; } = new List<Comment>();

    public virtual Movie Movie { get; set; } = null!;

    public virtual Comment? ParentComment { get; set; }

    public virtual User User { get; set; } = null!;
}
