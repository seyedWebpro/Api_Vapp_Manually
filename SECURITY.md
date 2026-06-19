# Security Policy

## Supported Versions

| Version | Supported |
|---------|-----------|
| `main` branch | ✅ Active development |

## Reporting a Vulnerability

**Please do not report security vulnerabilities through public GitHub Issues.**

If you discover a security issue, report it privately by emailing:

**support@vapp.ir**

Include:
- A description of the vulnerability
- Steps to reproduce
- Potential impact
- Suggested fix (if any)

We aim to acknowledge reports within **48 hours** and provide a status update within **7 days**.

## Security Best Practices for Deployments

- Never commit real credentials — use environment variables or a secrets manager
- Set `Development:DisableAuth` to `false` in production
- Restrict CORS to known frontend origins
- Use HTTPS in production
- Rotate JWT secrets and API keys regularly
- Keep .NET and NuGet packages up to date

## Known Configuration Risks

- `appsettings.json` may contain sensitive values — override them via environment variables in production
- Rate limiting middleware may be disabled by default — enable before public deployment
- File upload limit is set to 2 GB — validate file types server-side
