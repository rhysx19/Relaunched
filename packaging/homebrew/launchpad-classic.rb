# Homebrew cask for Launchpad Classic.
#
# To distribute via Homebrew, host this file in a tap repository named
# `homebrew-tap` under the same GitHub account (Casks/launchpad-classic.rb),
# update `version`, and replace `sha256 :no_check` with the zip's checksum:
#   shasum -a 256 Launchpad-Classic-macOS.zip
#
# Users then install with:
#   brew tap rhysx1/tap
#   brew install --cask launchpad-classic
cask "launchpad-classic" do
  version "1.0"
  sha256 :no_check

  url "https://github.com/rhysx1/Classic-Launchpad/releases/download/v#{version}/Launchpad-Classic-macOS.zip"
  name "Launchpad Classic"
  desc "Classic full-screen Launchpad-style app launcher overlay"
  homepage "https://github.com/rhysx1/Classic-Launchpad"

  app "Launchpad Classic.app"

  zap trash: [
    "~/Library/Preferences/com.rhys.MacOS-LaunchpadClassic.plist",
  ]
end
