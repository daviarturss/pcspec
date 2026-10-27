using System.Windows.Input;

namespace PcSpec.App.ViewModels;

/// <summary>Comando sincrono simples, com <c>CanExecute</c> reavaliado sob demanda.</summary>
public sealed class RelayCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;

    public void Execute(object? parameter) => execute();

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

/// <summary>
/// Comando assincrono: desabilita a si mesmo enquanto a tarefa roda, para não
/// disparar duas coletas (ou dois benchmarks) ao clicar duas vezes.
/// </summary>
public sealed class AsyncCommand(Func<Task> execute, Func<bool>? canExecute = null) : ICommand
{
    private bool _running;

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => !_running && (canExecute?.Invoke() ?? true);

    public async void Execute(object? parameter)
    {
        if (_running)
        {
            return;
        }

        _running = true;
        RaiseCanExecuteChanged();

        try
        {
            await execute();
        }
        catch (Exception ex)
        {
            // O ViewModel trata os erros esperados; isto e a rede de segurança para
            // que uma exceção inesperada não derrube o processo (async void).
            System.Diagnostics.Debug.WriteLine($"Comando falhou: {ex}");
        }
        finally
        {
            _running = false;
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
