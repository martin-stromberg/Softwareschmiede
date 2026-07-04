## Testing 
- Always run a full build before running tests. Never use --no-build. If E2E or unit tests fail, verify the build succeeded first before diagnosing as flakiness or pre-existing failures.
- This project requires .NET Desktop workload to run tests. Verify the correct test configuration before running; do not repeatedly retry a config the user has already flagged as broken.


## Sub-Agent / Lifecycle Workflow section

- Never trust sub-agent completion or test-pass reports at face value. Independently verify build cleanliness and test results (e.g., confirm files were actually created, code was actually removed) before reporting success to the user.