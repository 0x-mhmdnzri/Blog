namespace Blog.Domain.Aggregates.Posts;

public enum PostStatus
{
    Draft = 0,
    Scheduled = 1,
    Published = 2,
    Archived = 3,
    SoftDeleted = 4
}
