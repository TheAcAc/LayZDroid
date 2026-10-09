# LayZDroid 1.0.1

- Verify the LayZ connection before launching: pass the selected data folder, exact ADB client and isolated server together.
- Require an explicit connection acknowledgement; time out failed checks and leave an already-running LayZ connection alone.
- Add regression checks for connection routing and invalid acknowledgements.
- Correct the application and release version labels.

Pair with LayZ 1.5.7. Existing emulator instances and account storage are preserved; use your existing data folder.
