// "staging" is deliberately NOT listed as a semantic-release prerelease branch here: RC
// version determination for staging lives in staging-ci.yml's own "version" job, which
// invokes semantic-release with a --branches override against this same config instead of
// a second branch entry in this file (see ci-target-schema.md section 9 / deliverable 9).
module.exports = {
  branches: ["main"],
  tagFormat: "v${version}",
  plugins: [
    "@semantic-release/commit-analyzer",
    "@semantic-release/release-notes-generator"
  ]
};
