using System.Net;
using System.Net.Mail;

namespace Infrastructure.Services;

/// <summary>
/// Envoi d'emails via SMTP. Configuré par variables d'environnement :
/// SMTP_HOST, SMTP_PORT, SMTP_USER, SMTP_PASS, SMTP_FROM, SMTP_SSL (true/false).
/// Si SMTP_HOST n'est pas défini, l'envoi est ignoré silencieusement (retourne false)
/// pour ne pas bloquer l'inscription : le superadmin verra quand même la demande.
/// </summary>
public class EmailService
{
    public bool EstConfigure => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SMTP_HOST"));

    public bool Envoyer(string destinataire, string sujet, string corpsHtml)
    {
        var host = Environment.GetEnvironmentVariable("SMTP_HOST");
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(destinataire)) return false;

        try
        {
            var port = int.TryParse(Environment.GetEnvironmentVariable("SMTP_PORT"), out var p) ? p : 587;
            var user = Environment.GetEnvironmentVariable("SMTP_USER") ?? "";
            var pass = Environment.GetEnvironmentVariable("SMTP_PASS") ?? "";
            var from = Environment.GetEnvironmentVariable("SMTP_FROM") ?? user;
            var ssl = (Environment.GetEnvironmentVariable("SMTP_SSL") ?? "true").ToLower() != "false";

            using var client = new SmtpClient(host, port)
            {
                EnableSsl = ssl,
                Credentials = string.IsNullOrWhiteSpace(user) ? CredentialCache.DefaultNetworkCredentials : new NetworkCredential(user, pass)
            };
            using var msg = new MailMessage(from, destinataire, sujet, corpsHtml) { IsBodyHtml = true };
            client.Send(msg);
            return true;
        }
        catch
        {
            // Ne jamais faire échouer le flux fonctionnel à cause de l'email.
            return false;
        }
    }
}
