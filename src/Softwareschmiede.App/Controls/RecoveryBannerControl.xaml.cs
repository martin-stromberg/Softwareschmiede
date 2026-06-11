using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Softwareschmiede.App.Controls;

/// <summary>Banner-Control für Recovery-Kandidaten.</summary>
public sealed partial class RecoveryBannerControl : UserControl
{
    /// <summary>Dependency Property für die Anzahl der Recovery-Kandidaten.</summary>
    public static readonly DependencyProperty CandidateCountProperty =
        DependencyProperty.Register(
            nameof(CandidateCount),
            typeof(int),
            typeof(RecoveryBannerControl),
            new PropertyMetadata(0, OnCandidateCountChanged));

    /// <summary>Dependency Property ob Recovery-Kandidaten vorhanden sind.</summary>
    public static readonly DependencyProperty HasRecoveryCandidatesProperty =
        DependencyProperty.Register(
            nameof(HasRecoveryCandidates),
            typeof(bool),
            typeof(RecoveryBannerControl),
            new PropertyMetadata(false));

    /// <summary>Dependency Property für den Recovery-Command.</summary>
    public static readonly DependencyProperty RecoverCommandProperty =
        DependencyProperty.Register(
            nameof(RecoverCommand),
            typeof(ICommand),
            typeof(RecoveryBannerControl),
            new PropertyMetadata(null));

    /// <summary>Anzahl der Recovery-Kandidaten.</summary>
    public int CandidateCount
    {
        get => (int)GetValue(CandidateCountProperty);
        set => SetValue(CandidateCountProperty, value);
    }

    /// <summary>Gibt an, ob Recovery-Kandidaten vorhanden sind.</summary>
    public bool HasRecoveryCandidates
    {
        get => (bool)GetValue(HasRecoveryCandidatesProperty);
        private set => SetValue(HasRecoveryCandidatesProperty, value);
    }

    /// <summary>Command für die Wiederherstellung.</summary>
    public ICommand? RecoverCommand
    {
        get => (ICommand?)GetValue(RecoverCommandProperty);
        set => SetValue(RecoverCommandProperty, value);
    }

    /// <inheritdoc cref="RecoveryBannerControl"/>
    public RecoveryBannerControl()
    {
        InitializeComponent();
    }

    private static void OnCandidateCountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RecoveryBannerControl control && e.NewValue is int count)
        {
            control.HasRecoveryCandidates = count > 0;
        }
    }
}
