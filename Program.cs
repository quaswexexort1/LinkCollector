using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;


class Program
{
    static async Task Main()
    {
        var botClient = new TelegramBotClient("8042903478:AAFdE2ZEOspzdak7duR8wSEjr2PEv1xMBTM");

        using var cts = new CancellationTokenSource();

        botClient.StartReceiving(
            HandleUpdateAsync,
            HandleErrorAsync,
            new ReceiverOptions { AllowedUpdates = { } },
            cancellationToken: cts.Token);

        var me = await botClient.GetMeAsync();
        Console.WriteLine($"Бот запущен. Имя: {me.Username}");

        Console.WriteLine("Нажми Enter для остановки бота");
        Console.ReadLine();

        cts.Cancel();
    }

    static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Type != UpdateType.Message || update.Message.Text == null)
            return;

        var message = update.Message;
        var userId = message.From.Id;
        var text = message.Text.Trim();

        if (text.StartsWith("/links"))
        {
            // Выводим список ссылок пользователя
            var userLinks = LinkStorage.GetLinksByUser(userId);

            if (userLinks.Count == 0)
            {
                await botClient.SendTextMessageAsync(message.Chat.Id, "У тебя пока нет сохранённых ссылок.", cancellationToken: cancellationToken);
            }
            else
            {
                var response = "Твои сохранённые ссылки:\n";
                foreach (var link in userLinks)
                {
                    response += $"{link.AddedAt:yyyy-MM-dd HH:mm}: {link.Url}\n";
                }
                await botClient.SendTextMessageAsync(message.Chat.Id, response, cancellationToken: cancellationToken);
            }
            return;
        }

        // Проверяем, является ли текст ссылкой
        if (Uri.TryCreate(text, UriKind.Absolute, out Uri uriResult)
            && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
        {
            // Сохраняем ссылку
            var linkItem = new LinkItem
            {
                UserId = userId,
                Url = text,
                AddedAt = DateTime.UtcNow
            };

            await LinkStorage.AddLinkAsync(linkItem);

            await botClient.SendTextMessageAsync(message.Chat.Id, "Ссылка сохранена!", cancellationToken: cancellationToken);
        }
        else
        {
            await botClient.SendTextMessageAsync(message.Chat.Id, "Пожалуйста, отправь корректную ссылку или команду /links для просмотра сохранённых ссылок.", cancellationToken: cancellationToken);
        }
    }

    static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Ошибка: {exception.Message}");
        return Task.CompletedTask;
    }
}


public static class LinkStorage
{
    private static readonly string FilePath = "links.json";
    private static List<LinkItem> links = new List<LinkItem>();

    static LinkStorage()
    {
        LoadLinks();
    }

    private static void LoadLinks()
    {
        if (System.IO.File.Exists(FilePath))
        {
            var json = System.IO.File.ReadAllText(FilePath);
            links = JsonSerializer.Deserialize<List<LinkItem>>(json) ?? new List<LinkItem>();
        }
    }

    private static async Task SaveLinksAsync()
    {
        var json = JsonSerializer.Serialize(links, new JsonSerializerOptions { WriteIndented = true });
        await System.IO.File.WriteAllTextAsync(FilePath, json);
    }

    public static async Task AddLinkAsync(LinkItem link)
    {
        links.Add(link);
        await SaveLinksAsync();
    }

    public static List<LinkItem> GetLinksByUser(long userId)
    {
        return links.FindAll(l => l.UserId == userId);
    }
}



public class LinkItem
{
    public long UserId { get; set; }
    public string Url { get; set; }
    public DateTime AddedAt { get; set; }
}