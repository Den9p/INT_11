import logging
import os
from pathlib import Path
from dotenv import load_dotenv
from telegram import Update
from telegram.ext import Updater, CommandHandler, MessageHandler, Filters, ConversationHandler, CallbackContext
from telethon import TelegramClient
import asyncio

dotenv_path = Path('.env')
load_dotenv(dotenv_path=dotenv_path)

token = os.getenv('TOKEN')
api_id = os.getenv('API_ID')
api_hash = os.getenv('API_HASH')

commands = [
    ("/help", "Помощь по командам"),
    ("/check_members", "Получить участников группы")
]

# Подключаем логирование
logging.basicConfig(
    filename='logfile.txt', format='%(asctime)s - %(name)s - %(levelname)s - %(message)s', level=logging.INFO
)

logger = logging.getLogger(__name__)

# Создаем цикл событий asyncio
loop = asyncio.get_event_loop()

# Инициализируем клиент Telethon
client = TelegramClient('session_name', api_id, api_hash, loop=loop)

# Определяем стадии диалога
CHANNEL_NAME, = range(1)


# Функция для команды /help
def helpCommand(update: Update, context: CallbackContext):
    help_text = "Доступные команды:\n\n"
    for command, description in commands:
        help_text += f"{command} - {description}\n"
    update.message.reply_text(help_text)


# Функция для чтения username из файла
def read_usernames_from_file(filepath):
    usernames = set()
    if os.path.exists(filepath):
        with open(filepath, 'r', encoding='utf-8') as file:
            for line in file:
                usernames.add(line.strip())
    return usernames


# Асинхронная функция для получения участников группы
async def fetch_members(group_name: str, update: Update, context: CallbackContext):
    await client.start()
    with open('members.txt', 'w', encoding='utf-8') as file:
        async for dialog in client.iter_dialogs():
            if (dialog.is_group or dialog.is_channel) and dialog.entity.title == group_name:
                async for member in client.iter_participants(dialog):
                    # Записываем каждого участника в файл
                    if member.username:
                        file.write(f"{member.username}\n")

    # Чтение usernames из обоих файлов
    members_usernames = read_usernames_from_file('members.txt')
    company_usernames = read_usernames_from_file('membersCompany.txt')

    # Поиск уникальных usernames
    unique_usernames = members_usernames - company_usernames

    # Формирование отчета
    report = f"Общее количество участников группы: {len(members_usernames)}\n"
    report += f"Участники, которых нет в списке компании: {len(unique_usernames)}\n"

    update.message.reply_text(report)

    if unique_usernames:
        update.message.reply_text("Список участников, которых нет в списке компании:\n" + "\n".join(unique_usernames))


# Синхронная функция для получения участников группы
def check_members(update: Update, context: CallbackContext):
    update.message.reply_text(
        "Введите название группы:\n\n"
        "Примечание: После ввода названия группы авторизуйтесь на сервере где запущен бот."
    )
    return CHANNEL_NAME


def handle_channel_name(update: Update, context: CallbackContext):
    group_name = update.message.text
    loop.run_until_complete(fetch_members(group_name, update, context))
    return ConversationHandler.END


def cancel(update: Update, context: CallbackContext):
    update.message.reply_text('Операция отменена.')
    return ConversationHandler.END


def main():
    updater = Updater(token, use_context=True)

    # Получаем диспетчер для регистрации обработчиков
    dp = updater.dispatcher

    # Регистрируем обработчики команд
    dp.add_handler(CommandHandler("help", helpCommand))

    conv_handler = ConversationHandler(
        entry_points=[CommandHandler('check_members', check_members)],
        states={
            CHANNEL_NAME: [MessageHandler(Filters.text & ~Filters.command, handle_channel_name)]
        },
        fallbacks=[CommandHandler('cancel', cancel)]
    )

    dp.add_handler(conv_handler)

    # Запускаем бота
    updater.start_polling()

    # Останавливаем бота при нажатии Ctrl+C
    updater.idle()


if __name__ == '__main__':
    main()
