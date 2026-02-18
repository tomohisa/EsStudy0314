using Microsoft.JSInterop;

namespace EsCQRSQuestions.Web;

/// <summary>
/// Backward-compatible JS interop entrypoints.
/// Keeps older cached JS bundles from breaking the Blazor circuit.
/// </summary>
public static class ClientInteropFallback
{
    [JSInvokable("SetClientId")]
    public static void SetClientId(string _)
    {
        // Intentionally no-op. Current implementation manages client id fully in JS.
    }
}
