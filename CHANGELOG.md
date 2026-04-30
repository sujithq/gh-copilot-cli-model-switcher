# Changelog

## [2.0.0](https://github.com/sujithq/gh-copilot-cli-model-switcher/compare/copilot-byok-model-switcher-v1.1.1...copilot-byok-model-switcher-v2.0.0) (2026-04-30)


### ⚠ BREAKING CHANGES

* rename tool

### Features

* add Copilot setup steps workflow for Node.js and .NET projects ([28840ba](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/28840ba9819b6175a19663ff86378b3504c82a13))
* add manage command and keep list read-only ([d5731a1](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/d5731a18d7eead536b32cac1a67b19e55497e944))
* add multi-profile removal and settings-based dedupe ([1ff4e24](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/1ff4e2480547b0ce40b85aac2447fded9a4b4757))
* add support for importing profiles from Foundry deployments ([3cef4d3](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/3cef4d367b19095bfbd115c9b388c5e24c64c2f6))
* add use/remove action flow to list screen ([f322cff](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/f322cff60974ad0a8a37ebcc0042dfa3f5f305f9))
* dynamically discover installed MCP servers for compat mode prompt ([f30060d](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/f30060d1cfe5fe4fdc170fa46854bc0c6853071b))
* enhance CI/CD workflows and documentation for GitHub Packages integration ([18686f5](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/18686f559a1d38dde5c858df527b7d56d16ab7e7))
* enhance configuration management with isolated temp directories and new tests ([f1c14f1](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/f1c14f19f333568abadf4ef4e60c3f449a9f1b3d))
* enhance Foundry profile import support for Azure AI Services and improve command handling ([e2c681d](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/e2c681db1174280007ddc74e9c42d4471558b7dc))
* implement Foundry import helpers and enhance profile management with new tests ([e7e2749](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/e7e2749a1202bbb146c6039706d2fb9b4e2dc91b))
* import only chat-capable Foundry deployments ([5a8764f](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/5a8764f9086a80977fbba28dadc3b3385e2b002d))
* include add action in interactive manage flow ([1876c77](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/1876c77b4ffd581f79f3f53109105d378f76207f))
* interactive MCP server selection for Azure BYOK compat mode ([951618b](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/951618b5d6af5520a408ad2c1d77c16f6baca85f))
* interactive profile selection in list command; fix import-foundry for AIServices accounts; fix Windows token auth routing ([9a46ff8](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/9a46ff82fd0ed24759bfdc834e41242caa1c87a8))
* rename tool ([807e576](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/807e576bd91fe0abd4d291f4961e1e83063c2293))


### Bug Fixes

* **auth:** bearer-only env in azure token mode ([3102ec6](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/3102ec612184356efc9a977e7fbb18be20beed85))
* **auth:** set bearer token env var for azure cli token flow ([d6fee94](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/d6fee946e355e8ad2a8b970f44d7a1b971e130ed))
* **auth:** use bearer token env for Azure CLI flow; docs update ([6db7f5b](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/6db7f5b2fc1943bb170be0e2cff7e27940b4861a))
* **byok:** auto-disable MCP-heavy servers for Azure tool-limit compatibility ([f48e0f5](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/f48e0f5fb9f31113644b995e5caa95574fed94fe))
* detect embedding models by 'embed' prefix, not 'embedding' substring ([7c57971](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/7c57971a85f3ff70d80fd889690266c93ab973f5))
* **dotnet:** use real TTY for interactive gh copilot ([4bd3cb4](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/4bd3cb452adb88cea2f77b4816884cd6b855e286))
* enhance release workflow to support manual dispatch with custom release tags ([f5ce335](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/f5ce33559368b0311c17d80574507fabba419fb4))
* prioritize project MCP config discovery before user config ([9bb5b7b](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/9bb5b7b3b8f2db6dda111a393bea1178d3015292))
* publish dotnet tool to NuGet.org so it can be installed without authentication ([e9dd58e](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/e9dd58e1378992ca073f2780e4dd22e27f7ecd09))
* remove UTF-8 BOM from release-please-config.json ([43cb4a2](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/43cb4a20e4e17d0d13900ebedacbeafae9b0ad74))
* return fresh config clone in loadConfig/LoadConfig to prevent DEFAULT_CONFIG mutation ([2701a16](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/2701a16feed2b4f4dcf4697ad36ef26e3b350805))
* **run:** preserve interactive mode with compat flags and pass args via gh -- ([da164fe](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/da164fe953f6cd6c29637930b4f559ea79a1194f))
* set checkout ref to release tag and tighten SemVer regex ([6296b22](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/6296b223ead8432ee63d9525dbb1879b6e1b3b52))
* update command syntax to use prompt mode across documentation and examples ([bc19393](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/bc19393fcef2c7f20a9546a22e0c006cf133609b))
* use Azure deployment name as model for BYOK compatibility ([a5e05da](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/a5e05da64bc9b25420a32053c912e95d1cc8c0b9))
* use case-sensitive trim for apiKey and apiKeyEnv in BuildProfileSettingsKey ([eb8c8d8](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/eb8c8d868a85f64c0f1e942ff0e46d62b8a9b341))
* use case-sensitive trim for apiKey and apiKeyEnv in buildProfileSettingsKey (nodejs) ([04bf823](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/04bf8237a3ead60558e7b78172b632b01c29d32a))

## [1.1.1](https://github.com/sujithq/gh-copilot-cli-model-switcher/compare/gh-copilot-byok-v1.1.0...gh-copilot-byok-v1.1.1) (2026-04-29)


### Bug Fixes

* enhance release workflow to support manual dispatch with custom release tags ([f5ce335](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/f5ce33559368b0311c17d80574507fabba419fb4))
* publish dotnet tool to NuGet.org so it can be installed without authentication ([e9dd58e](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/e9dd58e1378992ca073f2780e4dd22e27f7ecd09))
* set checkout ref to release tag and tighten SemVer regex ([6296b22](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/6296b223ead8432ee63d9525dbb1879b6e1b3b52))

## [1.1.0](https://github.com/sujithq/gh-copilot-cli-model-switcher/compare/gh-copilot-byok-v1.0.0...gh-copilot-byok-v1.1.0) (2026-04-29)


### Features

* add Copilot setup steps workflow for Node.js and .NET projects ([28840ba](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/28840ba9819b6175a19663ff86378b3504c82a13))
* add manage command and keep list read-only ([d5731a1](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/d5731a18d7eead536b32cac1a67b19e55497e944))
* add multi-profile removal and settings-based dedupe ([1ff4e24](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/1ff4e2480547b0ce40b85aac2447fded9a4b4757))
* add support for importing profiles from Foundry deployments ([3cef4d3](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/3cef4d367b19095bfbd115c9b388c5e24c64c2f6))
* add use/remove action flow to list screen ([f322cff](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/f322cff60974ad0a8a37ebcc0042dfa3f5f305f9))
* dynamically discover installed MCP servers for compat mode prompt ([f30060d](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/f30060d1cfe5fe4fdc170fa46854bc0c6853071b))
* enhance CI/CD workflows and documentation for GitHub Packages integration ([18686f5](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/18686f559a1d38dde5c858df527b7d56d16ab7e7))
* enhance configuration management with isolated temp directories and new tests ([f1c14f1](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/f1c14f19f333568abadf4ef4e60c3f449a9f1b3d))
* enhance Foundry profile import support for Azure AI Services and improve command handling ([e2c681d](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/e2c681db1174280007ddc74e9c42d4471558b7dc))
* implement Foundry import helpers and enhance profile management with new tests ([e7e2749](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/e7e2749a1202bbb146c6039706d2fb9b4e2dc91b))
* import only chat-capable Foundry deployments ([5a8764f](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/5a8764f9086a80977fbba28dadc3b3385e2b002d))
* include add action in interactive manage flow ([1876c77](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/1876c77b4ffd581f79f3f53109105d378f76207f))
* interactive MCP server selection for Azure BYOK compat mode ([951618b](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/951618b5d6af5520a408ad2c1d77c16f6baca85f))
* interactive profile selection in list command; fix import-foundry for AIServices accounts; fix Windows token auth routing ([9a46ff8](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/9a46ff82fd0ed24759bfdc834e41242caa1c87a8))


### Bug Fixes

* **auth:** bearer-only env in azure token mode ([3102ec6](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/3102ec612184356efc9a977e7fbb18be20beed85))
* **auth:** set bearer token env var for azure cli token flow ([d6fee94](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/d6fee946e355e8ad2a8b970f44d7a1b971e130ed))
* **auth:** use bearer token env for Azure CLI flow; docs update ([6db7f5b](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/6db7f5b2fc1943bb170be0e2cff7e27940b4861a))
* **byok:** auto-disable MCP-heavy servers for Azure tool-limit compatibility ([f48e0f5](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/f48e0f5fb9f31113644b995e5caa95574fed94fe))
* detect embedding models by 'embed' prefix, not 'embedding' substring ([7c57971](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/7c57971a85f3ff70d80fd889690266c93ab973f5))
* **dotnet:** use real TTY for interactive gh copilot ([4bd3cb4](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/4bd3cb452adb88cea2f77b4816884cd6b855e286))
* prioritize project MCP config discovery before user config ([9bb5b7b](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/9bb5b7b3b8f2db6dda111a393bea1178d3015292))
* return fresh config clone in loadConfig/LoadConfig to prevent DEFAULT_CONFIG mutation ([2701a16](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/2701a16feed2b4f4dcf4697ad36ef26e3b350805))
* **run:** preserve interactive mode with compat flags and pass args via gh -- ([da164fe](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/da164fe953f6cd6c29637930b4f559ea79a1194f))
* use Azure deployment name as model for BYOK compatibility ([a5e05da](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/a5e05da64bc9b25420a32053c912e95d1cc8c0b9))
* use case-sensitive trim for apiKey and apiKeyEnv in BuildProfileSettingsKey ([eb8c8d8](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/eb8c8d868a85f64c0f1e942ff0e46d62b8a9b341))
* use case-sensitive trim for apiKey and apiKeyEnv in buildProfileSettingsKey (nodejs) ([04bf823](https://github.com/sujithq/gh-copilot-cli-model-switcher/commit/04bf8237a3ead60558e7b78172b632b01c29d32a))
