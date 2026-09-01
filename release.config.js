const path = require("node:path");

// "staging" is deliberately NOT listed as a semantic-release prerelease branch here: RC
// version determination for staging lives in staging-ci.yml's own "version" job, which
// invokes semantic-release with a --branches override against this same config instead of
// a second branch entry in this file (see ci-target-schema.md section 4.8).

// Single-asset shape (release.zip + update.json), unlike FinanceManager's per-runtime
// RELEASE_ASSET_PATHS array - Softwareschmiede only ever publishes one win-x64 zip, so
// RELEASE_ASSET_PATH (singular) is enough. See build-and-package/action.yml for why the
// asset name isn't platform-suffixed.
const releaseAssetPath = process.env.RELEASE_ASSET_PATH;
const releaseManifestPath = process.env.RELEASE_MANIFEST_PATH;
const releaseAssets = [releaseAssetPath, releaseManifestPath]
  .filter(Boolean)
  .map((assetPath) => ({ path: assetPath, name: path.basename(assetPath) }));

const releasePlugins = [
  "@semantic-release/commit-analyzer",
  "@semantic-release/release-notes-generator",
  [
    "@semantic-release/github",
    {
      assets: releaseAssets
    }
  ]
];

// Selected instead of releasePlugins whenever resolve-release-version.mjs runs its
// dry-run-only version check (RESOLVE_DRY_RUN=true, set in runSemanticReleaseDryRun()) -
// avoids loading @semantic-release/github (and its verifyConditions checks) for a call that
// never publishes anything, matching FinanceManager's release.config.js.
const dryRunPlugins = ["@semantic-release/commit-analyzer"];

module.exports = {
  branches: ["main"],
  tagFormat: "v${version}",
  plugins: process.env.RESOLVE_DRY_RUN === "true" ? dryRunPlugins : releasePlugins
};
