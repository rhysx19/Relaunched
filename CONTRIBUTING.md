# Contributing to Relaunched

Thanks for helping bring the classic Launchpad back! Contributions of all sizes are welcome — bug reports, fixes, and features.

## Building from source

```bash
xcode-select --install   # one-time: command-line tools
./build.sh               # compiles and ad-hoc signs Relaunched.app
open Relaunched.app
```

There is no Xcode project and there are no third-party dependencies — the app is plain Swift on system frameworks (SwiftUI, AppKit, Carbon, and a dynamically loaded MultitouchSupport for trackpad gestures). Please keep it that way unless there's a very strong reason.

## Pull requests

- Branch from `main` and keep PRs small and focused — one fix or feature per PR.
- CI must pass: the **Build macOS** workflow builds the full app bundle on every PR.
- PRs are **squash-merged**, so the PR title becomes the commit subject — write it accordingly (imperative mood, e.g. "Fix folder keyboard navigation").
- Match the existing style: four-space indentation, and comments that explain *why*, not *what*.
- If your change is visual, include a before/after screenshot in the PR description.

## Bugs and ideas

Open an issue with your macOS version and clear steps to reproduce. For feature ideas, a sentence or two on the motivation helps a lot.

## Releases (maintainers)

Push a `vX.Y` tag — CI builds, stamps the version, and publishes `Relaunched-macOS.zip` to the release automatically.
