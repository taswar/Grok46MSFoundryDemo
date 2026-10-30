# Contributing

Thanks for helping improve this sample. It's a .NET 10 console app with four use cases for Grok 4.6 on Microsoft Foundry, using `Microsoft.Extensions.AI`, the OpenAI .NET SDK and Entra ID.

## Ways to contribute

- **Report a bug.** A case fails, or the README doesn't match what the code does.
- **Fix the docs.** Typos, unclear steps, or a missing troubleshooting case.
- **Improve the sample.** For example, running one case from the command line.

For anything larger than a small fix, open an issue first.

## Set up

```bash
git clone https://github.com/taswar/Grok46MSFoundryDemo.git
cd Grok46MSFoundryDemo
dotnet build
```

To run it, deploy Grok 4.6 on Microsoft Foundry, set `AZURE_AI_ENDPOINT` (the `/models` endpoint) with user secrets, and sign in with `az login`, as described in the [README](README.md#configuration).

## Guidelines

- **Never commit secrets.** Settings belong in user secrets.
- **Keep Entra ID** with the `https://ai.azure.com/.default` scope. No API keys.
- **Keep the `/models` endpoint and `ApiVersionPolicy`.** Grok is a partner model, not an Azure OpenAI deployment.
- **Keep the tools read-only and fake.** Don't connect them to real systems.
- **Keep it a sample:** one short `Program.cs`, `net10.0`, nullable on, the existing style.
- **Keep the README in sync**, and one change per pull request.

## Pull request checklist

- [ ] `dotnet build` succeeds with no new warnings.
- [ ] Changed cases run against your own deployment, or you explain why not.
- [ ] No secrets or endpoints in the diff.

## Security issues

Please use **Security → Report a vulnerability** on the repo instead of a public issue.

## License

By contributing, you agree that your contributions are licensed under the [MIT License](LICENSE).
