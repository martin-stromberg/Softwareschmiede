using System.Threading;
using FluentAssertions;
using Softwareschmiede.App.ViewModels;
using Softwareschmiede.App.Views;

namespace Softwareschmiede.Tests.App.Views;

/// <summary>Tests für <see cref="UpdateProgressDialog"/>.</summary>
public sealed class UpdateProgressDialogTests
{
    /// <summary>Der Dialog lässt sich mit einem gebundenen <see cref="UpdateProgressViewModel"/> anzeigen, ohne dass die WPF-Databinding-Engine eine InvalidOperationException wirft (Regressionstest für Issue 135).</summary>
    [Fact]
    public void Show_ShouldNotThrowBindingException()
    {
        Exception? caughtException = null;
        var thread = new Thread(() =>
        {
            try
            {
                var viewModel = new UpdateProgressViewModel();
                var dialog = new UpdateProgressDialog
                {
                    DataContext = viewModel
                };

                dialog.Show();
                dialog.Close();
            }
            catch (Exception ex)
            {
                caughtException = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        caughtException.Should().BeNull();
    }
}
