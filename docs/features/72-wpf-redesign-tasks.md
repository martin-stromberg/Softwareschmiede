# Tasks: Projektdetailansicht

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | ViewModel | ProjektName-Eigenschaft zu ProjectDetailViewModel hinzufügen | Erledigt | Kein direkter Test |
| 2 | ViewModel | ProjektBeschreibung-Eigenschaft zu ProjectDetailViewModel hinzufügen | Erledigt | Kein direkter Test |
| 3 | ViewModel | SelectedRepository-Eigenschaft zu ProjectDetailViewModel hinzufügen | Erledigt | Kein direkter Test |
| 4 | ViewModel | AufgabenFilter-Eigenschaft zu ProjectDetailViewModel hinzufügen | Erledigt | Kein direkter Test |
| 5 | ViewModel | ZurueckCommand zu ProjectDetailViewModel hinzufügen | Erledigt | Kein direkter Test |
| 6 | ViewModel | SpeichernCommand zu ProjectDetailViewModel hinzufügen | Erledigt | ProjectDetailViewModelTests.ProjektSpeichernAsync_ErstelltNeuesProjekt_WennIdLeer |
| 7 | ViewModel | LoeschenCommand zu ProjectDetailViewModel hinzufügen | Erledigt | ProjectDetailViewModelTests.ProjektLoeschenAsync_Success_RuftDeleteAsyncUndZurueckActionAuf |
| 8 | ViewModel | FilterCommand zu ProjectDetailViewModel hinzufügen | Erledigt | Kein direkter Test |
| 9 | ViewModel | RepositoryZuweisenCommand zu ProjectDetailViewModel hinzufügen | Erledigt | ProjectDetailViewModelTests.RepositoryZuweisenAsync_Success_RuftAddRepositoryAsyncAuf |
| 10 | ViewModel | RepositoryOeffnenCommand zu ProjectDetailViewModel hinzufügen | Erledigt | ProjectDetailViewModelTests.RepositoryOeffnenAsync_Success_OeffnetRepositoryUrl |
| 11 | ViewModel | ProjektSpeichernAsync-Methode zu ProjectDetailViewModel hinzufügen | Erledigt | ProjectDetailViewModelTests.ProjektSpeichernAsync_AktualisiertBestehendesProjekt_WennIdVorhanden |
| 12 | ViewModel | ProjektLoeschenAsync-Methode zu ProjectDetailViewModel hinzufügen | Erledigt | ProjectDetailViewModelTests.ProjektLoeschenAsync_Aborted_RuftDeleteAsyncNichtAuf |
| 13 | ViewModel | RepositoryOeffnenAsync-Methode zu ProjectDetailViewModel hinzufügen | Erledigt | ProjectDetailViewModelTests.RepositoryOeffnenAsync_Success_OeffnetRepositoryUrl |
| 14 | ViewModel | LadenAsync-Methode in ProjectDetailViewModel erweitern | Erledigt | Kein direkter Test |
| 15 | UI | RepositoryAssignDialog.xaml erstellen | Erledigt | Kein direkter Test |
| 16 | UI | RepositoryAssignDialog.xaml.cs erstellen | Erledigt | Kein direkter Test |
| 17 | ViewModel | RepositoryAssignViewModel.cs erstellen | Erledigt | Kein direkter Test |
| 18 | UI | Ribbon-Menü zu ProjectDetailView.xaml hinzufügen | Erledigt | Kein direkter Test |
| 19 | UI | Projekt-Kachel zu ProjectDetailView.xaml hinzufügen | Erledigt | Kein direkter Test |
| 20 | UI | Aufgaben-Kachel zu ProjectDetailView.xaml hinzufügen | Erledigt | Kein direkter Test |
| 21 | UI | Filter-Overlay-Panel zu ProjectDetailView.xaml hinzufügen | Erledigt | Kein direkter Test |
| 22 | UI | Einfache Aufgabenliste aus ProjectDetailView.xaml entfernen | Erledigt | Kein direkter Test |
| 23 | Navigation | Zurück-Command mit Navigationssystem verbinden | Erledigt | Kein direkter Test |
| 24 | Tests | ProjektSpeichernAsync_Success-Test erstellen | Erledigt | ProjectDetailViewModelTests.ProjektSpeichernAsync_Success_RuftProjektHinzugefuegtCallbackAuf |
| 25 | Tests | ProjektSpeichernAsync_ValidationError-Test erstellen | Erledigt | ProjectDetailViewModelTests.ProjektSpeichernAsync_ValidationError_CanExecuteFalse_WennNameLeer |
| 26 | Tests | ProjektLoeschenAsync_Success-Test erstellen | Erledigt | ProjectDetailViewModelTests.ProjektLoeschenAsync_Success_RuftDeleteAsyncUndZurueckActionAuf |
| 27 | Tests | ProjektLoeschenAsync_Aborted-Test erstellen | Erledigt | ProjectDetailViewModelTests.ProjektLoeschenAsync_Aborted_RuftDeleteAsyncNichtAuf |
| 28 | Tests | RepositoryZuweisenAsync_Success-Test erstellen | Erledigt | ProjectDetailViewModelTests.RepositoryZuweisenAsync_Success_RuftAddRepositoryAsyncAuf |
| 29 | Tests | RepositoryOeffnenAsync_Success-Test erstellen | Erledigt | ProjectDetailViewModelTests.RepositoryOeffnenAsync_Success_OeffnetRepositoryUrl |
| 30 | E2E-Tests | Projekt bearbeiten und speichern E2E-Test erstellen | Erledigt | ProjectDetailE2ETests.ProjektBearbeitenUndSpeichern_AktualisierterNameBleibt_E2E |
| 31 | E2E-Tests | Projekt löschen E2E-Test erstellen | Erledigt | ProjectDetailE2ETests.ProjektLoeschen_BestaetigungErforderlichUndOverlayGeschlossen_E2E |
| 32 | E2E-Tests | Aufgabe neu anlegen E2E-Test erstellen | Erledigt | ProjectDetailE2ETests.AufgabeNeuAnlegen_ErscheintInAufgabenliste_E2E |
| 33 | E2E-Tests | Aufgaben filtern E2E-Test erstellen | Erledigt | ProjectDetailE2ETests.AufgabenFiltern_OverlayOeffnetUndSchliesst_E2E |
| 34 | E2E-Tests | Repository zuweisen E2E-Test erstellen | Erledigt | ProjectDetailE2ETests.RepositoryZuweisen_DialogOeffnetUndSchliessbarPerAbbrechen_E2E |
| 35 | E2E-Tests | Repository öffnen E2E-Test erstellen | Erledigt | ProjectDetailE2ETests.RepositoryOeffnen_ButtonExistiertInDetailansicht_E2E |
| 36 | E2E-Tests | Zurück zur Übersicht E2E-Test erstellen | Erledigt | ProjectDetailE2ETests.ZurueckZurUebersicht_SchliesstOverlayUndZeigtListe_E2E |
