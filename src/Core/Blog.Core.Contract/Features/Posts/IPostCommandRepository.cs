namespace Blog.Core.Contract.Features.Posts;

using Blog.Core.Domain.Entities.Post;

public interface IPostCommandRepository
{
    Task AddAsync(Post post, CancellationToken cancellationToken = default);
    Task<Post?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
