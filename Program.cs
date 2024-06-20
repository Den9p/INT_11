using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types;
using System.Diagnostics;
using static System.Formats.Asn1.AsnWriter;
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

string botToken = configuration["BotConfiguration:BotToken"];

var botClient = new TelegramBotClient(botToken);


using var cts = new CancellationTokenSource();

var receiverOptions = new ReceiverOptions
{
    AllowedUpdates = Array.Empty<UpdateType>()
};

botClient.StartReceiving(
    HandleUpdateAsync,
    HandleErrorAsync,
    receiverOptions,
    cancellationToken: cts.Token
);

Console.WriteLine("Бот запущен");

var exitEvent = new ManualResetEvent(false);
Console.CancelKeyPress += (sender, e) => {
    e.Cancel = true;
    cts.Cancel();
    exitEvent.Set();
};

exitEvent.WaitOne();

static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
{
    Console.WriteLine($"An error occurred: {exception.Message}");
    return Task.CompletedTask;
}



static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
{
    if (update.Type == UpdateType.Message && update.Message.Type == MessageType.ChatMembersAdded)
    {
        foreach (var member in update.Message.NewChatMembers)
        {
            var chatId = update.Message.Chat.Id;
            var userName = !string.IsNullOrEmpty(member.Username) 
                ? member.Username : "Unknown User";

            CreateOrExistFile(chatId);
            AppendUserNameToFile(chatId, userName);
        }
    }
    // Проверка на получение текстового сообщения в группе
    else if (update.Type == UpdateType.Message && update.Message.Type == MessageType.Text)
    {
        try
        {
            long chatId = update.Message.Chat.Id;
            var messageText = update.Message.Text;
            string userName = !string.IsNullOrEmpty(update.Message.From.Username) 
                ? update.Message.From.Username : "Unknown User";

            CreateOrExistFile(chatId);
            AppendUserNameToFile(chatId, userName);



            if (messageText == "/help")
            {
                await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: "/help - Помощь по командам\n" +
                          "/checkGroup - Проверка пользователей группы",
                    cancellationToken: cancellationToken
                );
            }

            if (messageText == "/checkGroup")
            {
                var nonCompanyUsers = GetNonCompanyUsers(chatId, GetCompanyUsers());

                if (nonCompanyUsers.Count > 0)
                {
                    // Получаем список администраторов чата
                    var admins = await botClient.GetChatAdministratorsAsync(chatId);

                    // Формируем сообщение с тегом администраторов
                    string adminTags = string.Join(" ", admins.Select(admin => $"@{admin.User.Username}"));
                    string notification = "Следующие пользователи не являются членами компании:\n" + string.Join("\n", nonCompanyUsers) + $"\n\n{adminTags}";

                    await botClient.SendTextMessageAsync(
                        chatId: chatId,
                        text: notification,
                        cancellationToken: cancellationToken
                    );
                }
                else
                {
                    await botClient.SendTextMessageAsync(
                        chatId: chatId,
                        text: "Все пользователи в этом чате являются членами компании.",
                        cancellationToken: cancellationToken
                    );
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Исключение: {ex.Message}");
        }
    }
}


static void CreateOrExistFile(long chatId)
{
    string filePath = Path.Combine(AppContext.BaseDirectory, $"{chatId}.txt");

    if (!System.IO.File.Exists(filePath))
    {
        using (var fs = System.IO.File.Create(filePath))
        {
            // Создаем файл и закрываем его сразу
        }
        Console.WriteLine($"Файл {chatId}.txt создан.");
    }
}

static void AppendUserNameToFile(long chatId, string userName)
{

    string filePath = Path.Combine(AppContext.BaseDirectory, $"{chatId}.txt");

    string[] lines = System.IO.File.ReadAllLines(filePath);

    // Проверяем, есть ли уже такое имя пользователя в файле
    if (Array.IndexOf(lines, userName) == -1)
    {
        // Добавляем имя пользователя в файл
        System.IO.File.AppendAllText(filePath, $"{userName}\n");
        Console.WriteLine($"Добавлено имя пользователя {userName} в файл {chatId}.txt");
    }
    else
    {
        Console.WriteLine($"Имя пользователя {userName} уже присутствует в файле {chatId}.txt");
    }
}



static List<string> GetNonCompanyUsers(long chatId, IEnumerable<string> companyUsers)
{
    string chatFilePath = Path.Combine(AppContext.BaseDirectory, $"{chatId}.txt");

    var chatUsers = new HashSet<string>(System.IO.File.ReadAllLines(chatFilePath));
    var nonCompanyUsers = chatUsers.Except(companyUsers).ToList();

    return nonCompanyUsers;
}

static HashSet<string> GetCompanyUsers()
{
    string companyFilePath = Path.Combine(AppContext.BaseDirectory, "membersCompany.txt");
    return new HashSet<string>(System.IO.File.ReadAllLines(companyFilePath));
}
