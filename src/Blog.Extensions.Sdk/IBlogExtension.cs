namespace Blog.Extensions.Sdk;

// Re-export plugin contracts for third-party authors (Artix-style public SDK).
global using Blog.Core.Contract.Primitives.Plugins;

/// <summary>Optional marker for extension packages.</summary>
public interface IBlogExtension : IBlogPlugin { }
