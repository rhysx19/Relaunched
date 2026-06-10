# Homebrew cask for Relaunched.
#
# To distribute via Homebrew, host this file in a tap repository named
# `homebrew-tap` under the same GitHub account (Casks/relaunched.rb),
# update `version`, and replace `sha256 :no_check` with the zip's checksum:
#   shasum -a 256 Relaunched-macOS.zip
#
# Users then install with:
#   brew tap rhysx19/tap
#   brew install --cask relaunched
cask "relaunched" do
  version "1.0"
  sha256 :no_check

  url "https://github.com/rhysx19/Relaunched/releases/download/v#{version}/Relaunched-macOS.zip"
  name "Relaunched"
  desc "The classic macOS Launchpad, brought back — full-screen app launcher overlay"
  homepage "https://github.com/rhysx19/Relaunched"

  app "Relaunched.app"

  zap trash: [
    "~/Library/Preferences/com.rhys.Relaunched.plist",
    # Pre-rebrand preferences ("Launchpad Classic")
    "~/Library/Preferences/com.rhys.MacOS-LaunchpadClassic.plist",
  ]
end
