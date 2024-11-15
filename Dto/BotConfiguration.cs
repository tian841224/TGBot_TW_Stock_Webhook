﻿namespace TGBot_TW_Stock_Webhook.Dto
{
    public class BotConfiguration
    {
        public string BotToken { get; init; } = default!;
        public Uri BotWebhookUrl { get; init; } = default!;
        public string SecretToken { get; init; } = default!;
    }
}
