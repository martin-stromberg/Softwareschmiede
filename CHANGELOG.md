# [1.6.0](https://github.com/martin-stromberg/Softwareschmiede/compare/v1.5.0...v1.6.0) (2026-07-11)


### Features

* Codex-Parameter vor automatischen Defaults schuetzen ([df30074](https://github.com/martin-stromberg/Softwareschmiede/commit/df30074d5d6e2c84341378ef2e9da92bc1dd0fa2))

# [1.5.0](https://github.com/martin-stromberg/Softwareschmiede/compare/v1.4.0...v1.5.0) (2026-07-11)


### Bug Fixes

* Fokus nach Promptvorlage auf Konsole setzen ([675bb7b](https://github.com/martin-stromberg/Softwareschmiede/commit/675bb7be6826c150d355472bb0d36144f8213eff))


### Features

* Promptvorlagen verwalten und senden ([6f9796d](https://github.com/martin-stromberg/Softwareschmiede/commit/6f9796d96d4c7f14174c7d07bb1fc97004850d6f))

# [1.4.0](https://github.com/martin-stromberg/Softwareschmiede/compare/v1.3.0...v1.4.0) (2026-07-10)


### Features

* korrigiere aufgabenliste im programmmenue ([1f83b83](https://github.com/martin-stromberg/Softwareschmiede/commit/1f83b83ad1a3876fa1ffd830bd6baccc9c9cc221))
* lasse aufgabenliste im menue wachsen ([31fc93d](https://github.com/martin-stromberg/Softwareschmiede/commit/31fc93d659eed8a086cdd6f6e79b98076b55e6fe))

# [1.3.0](https://github.com/martin-stromberg/Softwareschmiede/compare/v1.2.0...v1.3.0) (2026-07-10)


### Features

* beendete aufgaben in projektdetail trennen ([43dc04c](https://github.com/martin-stromberg/Softwareschmiede/commit/43dc04c345214a036c463f42557db500c49c80cc))
* Programmsymbol (Hammer-Icon) fuer Executable, Taskleiste und Fenstertitel ([64a5de1](https://github.com/martin-stromberg/Softwareschmiede/commit/64a5de1ded55dc645197f9568ee1a26955a70af6))

# [1.2.0](https://github.com/martin-stromberg/Softwareschmiede/compare/v1.1.0...v1.2.0) (2026-07-10)


### Bug Fixes

* ConPTY-Subscriber-Exception-Test wartet nie auf echten Prozess-Exit ([7f11896](https://github.com/martin-stromberg/Softwareschmiede/commit/7f11896c973530fb3842cbd0dd380b618823a1c7))
* E2E-Tests mit erwarteter Fehlermeldung meldeten Fehler als Fehler. ([20f40da](https://github.com/martin-stromberg/Softwareschmiede/commit/20f40da44956c90cd04ae58729a2d8976cd264b9))
* Flakiness in Clipboard-Paste-Tests durch fehlende Message-Pump beheben ([633c193](https://github.com/martin-stromberg/Softwareschmiede/commit/633c193e451d2db32bebfe103b4d5bb700ccb734))
* Kachel zeigte weiterhin "Laeuft" statt "Wartet auf Eingabe" (Issue 108) ([2b2143d](https://github.com/martin-stromberg/Softwareschmiede/commit/2b2143d666d47e50783c0682b881667a8c73b785))
* Seitenleisten-Kachel zeigte trotz laufender CLI weiterhin "Bereit" (Issue 108) ([e1fa9f9](https://github.com/martin-stromberg/Softwareschmiede/commit/e1fa9f98e68ed29a5a9c83ed638a2dcd498f5b37))
* TerminalControl gibt AutomationProperties.Name/HelpText nicht an UI Automation weiter ([c92989a](https://github.com/martin-stromberg/Softwareschmiede/commit/c92989aae6fbd2d6f93e571d3c22517c220eb9c4))


### Features

* Arbeitsstatus in Aufgabenliste automatisch aktualisieren ([aa3e034](https://github.com/martin-stromberg/Softwareschmiede/commit/aa3e034bea7600e16ca72787730b6f0af7683a97))

# [1.1.0](https://github.com/martin-stromberg/Softwareschmiede/compare/v1.0.1...v1.1.0) (2026-07-09)


### Bug Fixes

* Arbeitsverzeichnis-Aufloesung im InSourceDirectory-Modus und Remote-Verzeichnisstruktur fuer GitHub/BitBucket (Issue [#98](https://github.com/martin-stromberg/Softwareschmiede/issues/98)) ([5299724](https://github.com/martin-stromberg/Softwareschmiede/commit/5299724033dfee4417d7f9cc16b169e1a8cf2d1c))
* dispatch repository structure lookup ([add392d](https://github.com/martin-stromberg/Softwareschmiede/commit/add392d065a174698b3a0e0403cd39df75ed7597))


### Features

* Arbeitsverzeichnis nachtraeglich bearbeitbar, Verzeichnisstruktur-Abruf implementiert (Issue [#98](https://github.com/martin-stromberg/Softwareschmiede/issues/98)) ([46ea76c](https://github.com/martin-stromberg/Softwareschmiede/commit/46ea76ce58a3ab93cd30927e51da86f745a7292b))
* konfigurierbares Arbeitsverzeichnis fuer KI-Ausfuehrung ([3f0bd9c](https://github.com/martin-stromberg/Softwareschmiede/commit/3f0bd9c105b7e5308af3bd90d9f8f318526925b3))

## [1.0.1](https://github.com/martin-stromberg/Softwareschmiede/compare/v1.0.0...v1.0.1) (2026-07-08)


### Bug Fixes

* pull-requests-Berechtigung fuer Release-Workflow ergaenzen ([4b4ef25](https://github.com/martin-stromberg/Softwareschmiede/commit/4b4ef2522e66e0681e240716a74ba5a30bd1d3b9))

# 1.0.0 (2026-07-08)


### Bug Fixes

* Abruf der Repositories korrigiert ([c404346](https://github.com/martin-stromberg/Softwareschmiede/commit/c404346c65cd655dd457d3e5ccc3694374d185c3))
* Anpassung des BitBucket-Plugins an Unternehmensumgebung ([89993f2](https://github.com/martin-stromberg/Softwareschmiede/commit/89993f2bf780838807c11ecd6987855604eec105))
* Aufruf aus Menü öffnet nun die Aufgabe ([a59ad6d](https://github.com/martin-stromberg/Softwareschmiede/commit/a59ad6d7f83a1af755aea811b25452832eaa7d47))
* Auslesen der Aufgabenbeschreibung korrigiert. ([7d5d2ce](https://github.com/martin-stromberg/Softwareschmiede/commit/7d5d2ce42f5a27d8f09d7067708d805a0ebd5f36))
* Blazor-Stub entfernt, KI-Streaming-Regression behoben, Lifecycle-Bugfixes ([921b9be](https://github.com/martin-stromberg/Softwareschmiede/commit/921b9be37fd256015c877858b76dd6129b6c04dd))
* CLI-Fenster auch bei Reaktivierung eingebettet ([c7895da](https://github.com/martin-stromberg/Softwareschmiede/commit/c7895dab22cf78edfab9f50abe0a3ac70ceaad9d))
* Code-Review-Befunde aus ConPTY-Implementierung beheben ([a6c549b](https://github.com/martin-stromberg/Softwareschmiede/commit/a6c549bfb704a4ee4be8ccf0862e6ba8ab957637))
* Code-Review-Befunde der Aufgabendetailansicht behoben ([a3581b7](https://github.com/martin-stromberg/Softwareschmiede/commit/a3581b775b9e80a0c137915c81a0c3ce02cb0726))
* Dispose-Leak im ProjectListViewModel behoben und veraltete Favicon-Tests entfernt ([8e06e1c](https://github.com/martin-stromberg/Softwareschmiede/commit/8e06e1cda749ce043d6eb0745b7926655a9ce68d))
* Fehlende Services für Plugins bereitgestellt ([bbb1059](https://github.com/martin-stromberg/Softwareschmiede/commit/bbb10590932d959e7011f6349417925f745fc421))
* IntegrationTests an neue API angepasst (Enum-Umbenennung, geänderte Signaturen) ([b2dec8d](https://github.com/martin-stromberg/Softwareschmiede/commit/b2dec8d36b5aaf8b30224f8d19e5e57683a954f3))
* KannIssuesLaden-Binding, KannLoeschen-Logik, Bitbucket-Validierung, Migration-Lücken ([a83d42f](https://github.com/martin-stromberg/Softwareschmiede/commit/a83d42f1abd7981949307c3c0dd3ff955e376f58))
* Kontextkomprimierung aus Plan entfernt. ([ef3ce41](https://github.com/martin-stromberg/Softwareschmiede/commit/ef3ce41c47f769556d9a58ee984424dd6ca254b3))
* Korrektur beim CLI -Fenster einbetten ([0b69875](https://github.com/martin-stromberg/Softwareschmiede/commit/0b69875d396088f2bf2a6c66c25062b65209c42d))
* Korrektur des Issue-Abrufs auf Projektdetailseite ([fef6bf6](https://github.com/martin-stromberg/Softwareschmiede/commit/fef6bf62afe344c782d5108ced8f0ad5ae3b11f4))
* Korrektur des Klonens eines BitBucket-Reporisory ([410825c](https://github.com/martin-stromberg/Softwareschmiede/commit/410825cc06c6e5773f9c0bd6115fc89b38fa2b47))
* Kritische Ressourcen-Leaks im ConPTY-Lifecycle beheben ([a2ef4c4](https://github.com/martin-stromberg/Softwareschmiede/commit/a2ef4c47ebbcc1b86afaa39032b9df993dcaf089))
* Kundenfeedback zu aktiven Aufgaben umgesetzt und Code-Review-Befunde behoben ([eb2e7fd](https://github.com/martin-stromberg/Softwareschmiede/commit/eb2e7fd1f65fd5223ac150892074bb89a31161a3))
* Margin für Regsietrkarte auf Projektdetailansicht ([5f47fbc](https://github.com/martin-stromberg/Softwareschmiede/commit/5f47fbc236d3f7e417219126854921d1eba4a798))
* Mergekonflikte behoben ([482d948](https://github.com/martin-stromberg/Softwareschmiede/commit/482d948d9e681ffd4cc75f9d8fd2f54b82e407b0))
* P/Invoke-Bugs in PseudoConsoleProcessStarter behoben, Korrekturen nachgezogen ([529407b](https://github.com/martin-stromberg/Softwareschmiede/commit/529407bb075790032bcb87b42212adc2f4c99842))
* Projektverzeichnis-Navigation navigiert zur Startseite statt zur Aufgabendetailseite ([55cb251](https://github.com/martin-stromberg/Softwareschmiede/commit/55cb2514efa7107237663448e8490d9aac125fec))
* Resolve merge conflict and race condition in lifecycle-orchestrator process termination ([c76f7ac](https://github.com/martin-stromberg/Softwareschmiede/commit/c76f7acae6a885180dcd1693c2dc2f31b01f2b03))
* Ribbon-Menü in den Einstellungen ([420d007](https://github.com/martin-stromberg/Softwareschmiede/commit/420d007eb8c2f479480f0888a284878b9fa35ef2))
* Robustheit des Aufgabenworkflows verbessern (Bugfixes aus Code-Review) ([c4743f3](https://github.com/martin-stromberg/Softwareschmiede/commit/c4743f3f8a6eac980c31a5a3e77949df0917eb40))
* Solution repariert ([dae7d36](https://github.com/martin-stromberg/Softwareschmiede/commit/dae7d3634abf16abb42b45180d7acb0ee2d7a8a4))
* Speichern der KI-Einstellungen korrigiert ([e7ac964](https://github.com/martin-stromberg/Softwareschmiede/commit/e7ac9644cc00418040813f93222690d19063961a))
* Terminal nach Session-Ende sofort neu rendern ([4063c12](https://github.com/martin-stromberg/Softwareschmiede/commit/4063c1248099fbb1c09d09fa962d4ffd1b67ab00))
* Terminal nach Stoppen der CLI leeren ([56dde0b](https://github.com/martin-stromberg/Softwareschmiede/commit/56dde0b67c2cd11d6182d93d1c41fd95aa0a173f))
* Terminal-Bildschirminhalt nach Navigation wiederherstellen ([fc5ca3e](https://github.com/martin-stromberg/Softwareschmiede/commit/fc5ca3ec7acf6b6e5f678cb0cfd089653a0c9cd0))
* tests korrigiert, einheitliche styles für komponenten, weitere befunde korrigiert ([81830a3](https://github.com/martin-stromberg/Softwareschmiede/commit/81830a362da31c7ad85e3dbcceca5dca0d6842ea))
* Tests repariert ([987e400](https://github.com/martin-stromberg/Softwareschmiede/commit/987e4008827e3e3560e14fa2927f65a1c971f2cf))
* Tests repariert ([debc203](https://github.com/martin-stromberg/Softwareschmiede/commit/debc203229932ea5d03e0fc2f0d99af1a9b6e0a7))
* ThreadPool-Blockierung beheben ([2fbe553](https://github.com/martin-stromberg/Softwareschmiede/commit/2fbe553058e9f56e08b17f53a426f32415b55a78))
* use repository-specific SCM plugin when starting task ([874b15c](https://github.com/martin-stromberg/Softwareschmiede/commit/874b15c40e226e876a0f54bc16f40a5c55714713))
* Weitere Code-Review-Befunde beheben (Iteration 2) ([b90ad70](https://github.com/martin-stromberg/Softwareschmiede/commit/b90ad709cc12146ca966b4859a1f00e0f53a7479))
* WPF-App stabilisiert – DI-Fehler, Memory-Leaks und Win32-Bugs behoben; E2E-Tests ergänzt ([c3737e0](https://github.com/martin-stromberg/Softwareschmiede/commit/c3737e0ab14459a86c77c9efbd5fe61f0a25b982))


### Features

* 'CLI starten'-Button im Ribbon für gestoppte Sessions ([bbdf202](https://github.com/martin-stromberg/Softwareschmiede/commit/bbdf202429c359db4cf288dd52ad3c9574d0ade5))
* Absturzstabilisierung und Beseitigung von Fehlerpotentialen ([eca2710](https://github.com/martin-stromberg/Softwareschmiede/commit/eca271099042ba8eb14335653d068200ac4dece6))
* Aktive Aufgaben in Seitenleiste und Dashboard anzeigen ([624dcfc](https://github.com/martin-stromberg/Softwareschmiede/commit/624dcfc372708c52048a1f89cff492106ea29d91))
* Arbeitsstatus in Fußleiste ([a0edeff](https://github.com/martin-stromberg/Softwareschmiede/commit/a0edeff1415030b6bd351fd267e60e913dbea1cc))
* Aufgabendetailansicht als separate fensterumfassende View statt inline-Einbettung ([523006e](https://github.com/martin-stromberg/Softwareschmiede/commit/523006e2955439bd43548be2b64bfea6fe8e0b7b))
* Aufgabendetailansicht mit Ribbon-Menü und statusabhängigem Content-Switching ([7c86f98](https://github.com/martin-stromberg/Softwareschmiede/commit/7c86f985e3a74575e71af145c7a86fae62657756))
* Aufgabenworkflow vereinfachen (Status entfernen, kombinierter Start) ([634cd0e](https://github.com/martin-stromberg/Softwareschmiede/commit/634cd0e40a46b72643d8cf958ffae2f5a991168b))
* BitBucket Plugin – Self-Hosted-Unterstützung, Settings-Fix und Tests ([f11840f](https://github.com/martin-stromberg/Softwareschmiede/commit/f11840f68141fcae93602b5170ca598562940e91))
* Branchname in Fußzeile ([16154d4](https://github.com/martin-stromberg/Softwareschmiede/commit/16154d40707278bd127651c114474baac920730f))
* CI/CD-Pipeline ([df91506](https://github.com/martin-stromberg/Softwareschmiede/commit/df91506bf331235e2ab7420beeedd6a7433173d4))
* Codex CLI Plugin integrieren ([05c4758](https://github.com/martin-stromberg/Softwareschmiede/commit/05c4758d1134be45625a81068f2703218b4c71c3))
* ConPTY-basiertes Terminal-Control als Ersatz für SetParent-Einbettung ([fa5775c](https://github.com/martin-stromberg/Softwareschmiede/commit/fa5775c2241910a6691f58ba9ef7bd90a28d5784))
* Einstellungsansicht mit Plugin-Registerkarten und globalen Dark-Mode-Styles ([2324ceb](https://github.com/martin-stromberg/Softwareschmiede/commit/2324ceb9c479556f5470c2437146fe3410e12640))
* FlaUI-E2E-Tests implementiert und Code-Review-Befunde behoben ([d22bc2c](https://github.com/martin-stromberg/Softwareschmiede/commit/d22bc2c4ebf36cff485e65780396a95e5700744c))
* Hook für Xml-Kommentarprüfung erweitert ([1f36d8e](https://github.com/martin-stromberg/Softwareschmiede/commit/1f36d8e8e18a6d17d7369d206cb79d57b5a24f76))
* issue.md beim Repository-Klon erstellen und in .gitignore eintragen ([ff9c104](https://github.com/martin-stromberg/Softwareschmiede/commit/ff9c104d924311a2b534fab17b6612a18a3b3e2a))
* kommandozeilenparameter für CLI-Plugins ([eb78932](https://github.com/martin-stromberg/Softwareschmiede/commit/eb78932785810aa7c2018b55c0693536d5b2358b))
* Parallele CLI-Ausführungen ohne Blockade bei verborgener Aufgabenseite ([a9e8d0b](https://github.com/martin-stromberg/Softwareschmiede/commit/a9e8d0b330727093d9a6ed5dcb69bd98e2d7c689))
* Plugin-Befehl nach cmd.exe-Start senden ([44585df](https://github.com/martin-stromberg/Softwareschmiede/commit/44585dfc852d2fb1d41b208c144e75068e83ab6a))
* Projektdetailansicht mit Ribbon-Menü, Kacheln und Repository-Verwaltung ([1abb2d9](https://github.com/martin-stromberg/Softwareschmiede/commit/1abb2d998c75bb9dcdf131335c72753219951295))
* Repository-Vorschlagspanel auf der Projektübersicht ([a7ab4e5](https://github.com/martin-stromberg/Softwareschmiede/commit/a7ab4e5bfcf2bc12aed51662575f268b06afcf50))
* SCM-Issue als Aufgabenvorschläge in Projektdetailansiht integriert ([6ad28df](https://github.com/martin-stromberg/Softwareschmiede/commit/6ad28dfcf47941a9f5f3bf8ed14624e2574d3f92))
* Terminal-Buffer-Synchronisierung stabilisiert und Clipboard-Paste (Ctrl+V) ergaenzt ([3821683](https://github.com/martin-stromberg/Softwareschmiede/commit/3821683d987182268ab5854d65dc872ff8d6fbac))
* WPF-Desktopanwendung – Bugfixes, Code-Review-Bereinigung und API-Stabilisierung ([0e01cc8](https://github.com/martin-stromberg/Softwareschmiede/commit/0e01cc8edf8c1d3922c593917d81ed2a6a264220))
* WPF-Desktopanwendung als nativer Windows-Client implementiert ([f72b103](https://github.com/martin-stromberg/Softwareschmiede/commit/f72b1038e768a3edb78eaaf9301791e2596a0f99))
* WPF-Plugin-Verfügbarkeit — SCM-Plugin-Auswahl im Repository-Zuweisung-Dialog ([12886e4](https://github.com/martin-stromberg/Softwareschmiede/commit/12886e4a30230642a0096e6cad25e89f98ce5684))
* WPF-Projektübersicht und -detail überarbeitet mit IDialogService, Ribbonmenü und Titelaktualisierung ([ab89053](https://github.com/martin-stromberg/Softwareschmiede/commit/ab89053cd963abb9192b35b994f66fe7dde9fc72))
