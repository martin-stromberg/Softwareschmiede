using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Softwareschmiede.Tests")]
// Ermoeglicht Softwareschmiede.App, den "--conpty-probe"-Diagnosemodus in App.xaml.cs mit denselben
// internen ConPTY-Primitiven (PseudoConsole, PseudoConsoleSession, PseudoConsoleProcessStarter) zu
// implementieren wie ConPtyEnvironmentProbe im Testprojekt - siehe dortigen Kommentar zur
// Prozessbaum-Tiefe (App.exe muss als unmittelbarer Elternprozess des ConPTY-Kindes auftreten,
// nicht der Test-Host).
[assembly: InternalsVisibleTo("Softwareschmiede.App")]
