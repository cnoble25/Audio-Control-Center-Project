# Memory Leak Fixes for Audio Control Center

## Critical Fixes Needed

### 1. Store Event Handler References for Cleanup

**Problem**: Anonymous lambdas in event handlers capture entire object graphs.

**Fix**: Store handler references and unsubscribe in `ResetSliders()` and `Dispose()`:

```csharp
// In SliderClass, add:
private EventHandler? _pickerFocusedHandler;
private EventHandler? _pickerSelectedChangedHandler;
private EventHandler? _sliderValueChangedHandler;

// When creating handlers, store them:
_pickerFocusedHandler = async (sender, e) => { ... };
applicationPicker.Focused += _pickerFocusedHandler;

// In cleanup:
if (applicationPicker != null && _pickerFocusedHandler != null)
{
    applicationPicker.Focused -= _pickerFocusedHandler;
    _pickerFocusedHandler = null;
}
```

### 2. Clear Static References

**Problem**: Static fields hold references indefinitely.

**Fix**: Clear static references when disposing:

```csharp
public void Dispose()
{
    // ... existing code ...
    
    // Clear static references
    if (Instance == this)
    {
        Instance = null;
    }
    sliders = null;
    Applications = null;
}
```

### 3. Unsubscribe Settings Page Handler

**Problem**: `Disappearing` handler may not be unsubscribed.

**Fix**: In `MainPage.xaml.cs`, unsubscribe when page is disposed:

```csharp
protected override void OnDisappearing()
{
    base.OnDisappearing();
    if (_disappearingHandler != null && settingsPage != null)
    {
        settingsPage.Disappearing -= _disappearingHandler;
        _disappearingHandler = null;
    }
}
```

### 4. Weak References for Static Data

**Problem**: Static arrays hold strong references.

**Fix**: Consider using `WeakReference` or clearing when not needed:

```csharp
public static void ClearStaticReferences()
{
    sliders = null;
    Applications = null;
    GC.Collect(); // Force cleanup
}
```

### 5. Cancel Long-Running Tasks

**Problem**: `Task.Run` without cancellation tokens.

**Fix**: Use `CancellationTokenSource`:

```csharp
private CancellationTokenSource? _cancellationTokenSource = new();

Task.Run(() => {
    // ... work ...
}, _cancellationTokenSource.Token);

// In Dispose:
_cancellationTokenSource?.Cancel();
_cancellationTokenSource?.Dispose();
```

### 6. Reduce Closure Captures

**Problem**: Closures capture entire objects.

**Fix**: Extract only needed values:

```csharp
// Instead of:
MainThread.BeginInvokeOnMainThread(() => {
    if (sliders[i] != null) { ... }
});

// Do:
var sliderValue = sliders[i]?.Value ?? 0;
MainThread.BeginInvokeOnMainThread(() => {
    // Use sliderValue instead of accessing sliders[i]
});
```

## Priority Order

1. **HIGH**: Fix event handler unsubscription (items 1, 3)
2. **HIGH**: Clear static references (item 2)
3. **MEDIUM**: Add cancellation tokens (item 5)
4. **LOW**: Optimize closures (item 6)
