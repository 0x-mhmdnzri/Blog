namespace Blog.Core.Contract.Primitives.Handlers;

/// <summary>Command marker (Artix: MediatR IRequest optional later).</summary>
public interface ICommand { }

public interface ICommand<TResult> { }
