// Re-export plugin contracts for third-party authors (Artix-style public SDK).
using Blog.Core.Contract.Primitives.Plugins;

namespace Blog.Extensions.Sdk;




/// <summary>Optional marker for extension packages.</summary>
public interface IBlogExtension : IBlogPlugin { }
