using BLL.Options;
using BLL.Service;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Walkamon.ModelBinding;

public sealed class VietnamDateTimeModelBinderProvider : IModelBinderProvider
{
    private readonly IModelBinder _binder;

    public VietnamDateTimeModelBinderProvider(TimePresentationOptions options)
    {
        _binder = new VietnamDateTimeModelBinder(options);
    }

    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var modelType = Nullable.GetUnderlyingType(context.Metadata.ModelType)
                        ?? context.Metadata.ModelType;
        return modelType == typeof(DateTime) ? _binder : null;
    }
}

internal sealed class VietnamDateTimeModelBinder : IModelBinder
{
    private readonly TimeSpan _offset;

    public VietnamDateTimeModelBinder(TimePresentationOptions options)
    {
        _offset = TimeSpan.FromMinutes(options.OffsetMinutes);
    }

    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);
        var valueResult = bindingContext.ValueProvider.GetValue(
            bindingContext.ModelName);
        if (valueResult == ValueProviderResult.None)
            return Task.CompletedTask;

        bindingContext.ModelState.SetModelValue(
            bindingContext.ModelName,
            valueResult);
        var raw = valueResult.FirstValue;
        var isNullable =
            Nullable.GetUnderlyingType(bindingContext.ModelType) != null;
        if (string.IsNullOrWhiteSpace(raw) && isNullable)
        {
            bindingContext.Result = ModelBindingResult.Success(null);
            return Task.CompletedTask;
        }

        if (raw != null &&
            VietnamDateTimeJsonConverter.TryParseToUtc(
                raw,
                _offset,
                out var parsed))
        {
            bindingContext.Result = ModelBindingResult.Success(parsed);
            return Task.CompletedTask;
        }

        bindingContext.ModelState.TryAddModelError(
            bindingContext.ModelName,
            "A valid ISO-8601 timestamp is required.");
        return Task.CompletedTask;
    }
}
