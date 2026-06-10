import { WebSocketServer } from "ws";
import pty from "node-pty";
import os from "os";

const wss = new WebSocketServer({ port: 3001 });

wss.on("connection", (socket) => {
    console.log("Client connected");

    let ptyProcess = null;

    socket.on("message", (msg) => {
        msg = msg.toString();

        // 1) Arbeitsverzeichnis setzen
        if (msg.startsWith("SET_CWD:")) {
            const cwd = msg.replace("SET_CWD:", "").trim();

            console.log("Starting shell in:", cwd);

            const shell = os.platform() === "win32" ? "powershell.exe" : "bash";

            ptyProcess = pty.spawn(shell, ["-NoExit"], {
                name: "xterm-color",
                cols: 120,
                rows: 30,
                cwd: cwd,
                env: process.env
            });

            ptyProcess.onData((data) => {
                socket.send(data);
            });

            return;
        }

        // 2) CLI starten
        if (msg.startsWith("START_CLI:")) {
            const cli = msg.replace("START_CLI:", "").trim();

            if (cli === "copilot") {
                ptyProcess.write("copilot\r");
            }

            if (cli === "claude") {
                ptyProcess.write("claude\r");
            }

            return;
        }

        // 3) Normale Eingaben weiterleiten
        if (ptyProcess) {
            ptyProcess.write(msg);
        }
    });

    socket.on("close", () => {
        if (ptyProcess) ptyProcess.kill();
    });
});

console.log("Terminal backend running on ws://localhost:3001");
