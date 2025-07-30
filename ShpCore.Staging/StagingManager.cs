using ShpCore.Logging;

namespace MSharp.ModLoader.StagingSystem;

// -- Aca manejo el staging de los payloads en base a las respuestas del adapter --

public class StagingManager<T>
{
    private readonly Stack<T> _history = new(); // El registro de cambios (en RAM)
    private T? _current; // Estado actual del payload
    private readonly Action<T> _applyCallback; //
    private readonly Action<T> _rollbackCallback;

    public StagingManager(Action<T> applyCallback, Action<T> rollbackCallback)
    {
        _applyCallback = applyCallback;
        _rollbackCallback = rollbackCallback;
    }

    public void MSadd(T next)
    {
        if (_current != null) _history.Push(_current); _current = next;

        try
        {
            _applyCallback(_current); // Aplicamos el cambio actual
            KernelLog.Debug("[Staging] Instruction added succesfully");
        }
        catch (Exception)
        {
            MSrevert(); // Automático por si la aplicación falla
            KernelLog.Panic("[Staging StagingManager line:32] Err with instruction commit, Rollbacking...");

            _current = default;
            _history.Clear();
            return;
        }
    }

    public void MSrevert()
    {
        if (_history.Count == 0)
        {
            KernelLog.Panic("[Staging Staging	Manager line:44 ] No previous verions to rollback.");
            return;
        }

        // Rollbackear sería limpiar la pila y volver al último estado válido
        KernelLog.Debug("[Staging] Reverting to last valid status");

        _current = _history.Pop();
        _rollbackCallback(_current);

        KernelLog.Debug("[Staging] Rollback OK");
        KernelLog.Info($"[Staging] Current Status: {_current}");
    }

    // Confirmamos estado actual como final. Aca el commit es simplemente dejar current como está y limpiar la pila.
    public void MScommit() => _history.Clear();

    // Devuelve el estado actual, o null si no hay ninguno - es como una memoria ram
    public T? MSgetCurrent() => _current;
}
