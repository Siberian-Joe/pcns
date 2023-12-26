using System.Diagnostics;
using System.Text.Json;
using MPI;
using Environment = MPI.Environment;

try
{
    // Создание MPI окружения и инициализация среды выполнения.
    using (new Environment(ref args))
    {
        // Получение глобального коммуникатора MPI.
        Intracommunicator world = Communicator.world;
        int numProcesses = world.Size; // Общее количество процессов.
        int processRank = world.Rank; // Ранг текущего процесса.

        Stopwatch stopwatch = new Stopwatch(); // Инициализация счетчика времени.

        // Загрузка данных выполняется только в мастер-процессе (ранг 0).
        List<string?> tweets = new List<string?>();
        if (processRank == 0)
        {
            stopwatch.Start();
            string filePath = "FIFA.csv"; // Путь к файлу с данными. Укажите свой путь.
            tweets = LoadData(filePath);
        }

        // Распространение данных всем процессам.
        world.Broadcast(ref tweets, 0);

        // Определение количества потоков для каждого процесса (2, 4, 6 или 8).
        int numThreads = 2 * (processRank + 1);

        // Разделение данных на части для каждого процесса.
        List<List<string?>> dataChunks = PartitionData(tweets, numProcesses);

        // Создание списков для хранения результатов обработки данных.
        List<string> resultA = new List<string>();
        List<string> resultB = new List<string>();
        List<string> resultMatchingCapitals = new List<string>();

        // Получение списка 10 наиболее упоминаемых хештегов (список А).
        List<string> topHashtags = GetTopHashtags(tweets);

        // Получение списка 10 столиц, из которых твиты отправлялись наиболее часто (список B).
        List<string> topCapitals = GetTopCapitalCities(tweets);

        // Параллельная обработка данных внутри каждого процесса с использованием нескольких потоков.
        Parallel.ForEach(dataChunks[processRank], new ParallelOptions { MaxDegreeOfParallelism = numThreads }, tweet =>
        {
            // Обработка каждого твита и обновление результатов (A, B и matchingCapitals).
            // Вызов соответствующих функций для обновления результатов.

            // Пример обновления результата A (наиболее упоминаемые хештеги).
            List<string> hashtagsInTweet = ExtractHashtags(tweet);
            lock (resultA) // Блокировка для многопоточной безопасности.
            {
                foreach (string hashtag in hashtagsInTweet)
                {
                    if (topHashtags.Contains(hashtag))
                    {
                        resultA.Add(hashtag);
                    }
                }
            }

            // Пример обновления результата B (столицы, из которых твиты отправлялись наиболее часто).
            string capitalOfTweet = ExtractCapital(tweet);
            if (!string.IsNullOrEmpty(capitalOfTweet))
            {
                lock (resultB) // Блокировка для многопоточной безопасности.
                {
                    if (topCapitals.Contains(capitalOfTweet))
                    {
                        resultB.Add(capitalOfTweet);
                    }
                }
            }

            // Пример обновления результата matchingCapitals (столицы, в которых используются хештеги из списка topHashtags).
            lock (resultMatchingCapitals) // Блокировка для многопоточной безопасности.
            {
                foreach (string hashtag in hashtagsInTweet)
                {
                    if (topHashtags.Contains(hashtag) && !string.IsNullOrEmpty(capitalOfTweet))
                    {
                        string capitalHashtagPair = $"{capitalOfTweet}|{hashtag}";
                        resultMatchingCapitals.Add(capitalHashtagPair);
                    }
                }
            }
        });

        // Сериализация результатов в JSON.
        byte[] resultABytes = SerializeObject(resultA);
        byte[] resultBBytes = SerializeObject(resultB);
        byte[] resultMatchingCapitalsBytes = SerializeObject(resultMatchingCapitals);

        // Мастер-процесс (ранг 0) собирает сериализованные результаты от всех процессов.
        byte[][] allResultABytes = world.Gather(resultABytes, 0);
        byte[][] allResultBBytes = world.Gather(resultBBytes, 0);
        byte[][] allResultMatchingCapitalsBytes = world.Gather(resultMatchingCapitalsBytes, 0);

        // Десериализация результатов.
        List<string> finalResultA = new List<string>();
        List<string> finalResultB = new List<string>();
        List<string> finalResultMatchingCapitals = new List<string>();

        if (processRank == 0)
        {
            foreach (var bytes in allResultABytes)
            {
                finalResultA.AddRange(DeserializeObject<List<string>>(bytes));
            }

            foreach (var bytes in allResultBBytes)
            {
                finalResultB.AddRange(DeserializeObject<List<string>>(bytes));
            }

            foreach (var bytes in allResultMatchingCapitalsBytes)
            {
                finalResultMatchingCapitals.AddRange(DeserializeObject<List<string>>(bytes));
            }

            // Завершающая обработка результатов и вывод.
            Console.WriteLine("Список А (10 наиболее упоминаемых хештегов):");
            foreach (string hashtag in finalResultA)
            {
                Console.WriteLine(hashtag);
            }

            Console.WriteLine("\nСписок B (10 столиц, из которых твиты отправлялись наиболее часто):");
            foreach (string capital in finalResultB)
            {
                Console.WriteLine(capital);
            }

            Console.WriteLine("\nСтолицы из списка B, в которых самый часто используемый хештег принадлежит списку A:");
            foreach (string matchingCapital in finalResultMatchingCapitals)
            {
                Console.WriteLine(matchingCapital);
            }

            stopwatch.Stop();

            TimeSpan elapsed = stopwatch.Elapsed;
            Console.WriteLine($"Время выполнения программы: {elapsed.TotalMilliseconds} миллисекунд");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Ошибка при выполнении программы: {ex.Message}");
}

// Метод для загрузки данных из текстового файла.
// Принимает путь к файлу 'filePath' и возвращает список строк (твитов).
List<string> LoadData(string filePath)
{
    List<string> tweets = new List<string>(); // Создаем список для хранения твитов.

    try
    {
        // Используем 'using' для гарантированного освобождения ресурсов после чтения файла.
        using (var reader = new StreamReader(filePath))
        {
            while (!reader.EndOfStream) // Пока не достигнут конец файла.
            {
                string line = reader.ReadLine(); // Считываем строку из файла.
                tweets.Add(line); // Добавляем считанную строку (твит) в список 'tweets'.
            }
        }
    }
    catch (Exception ex)
    {
        // Если возникает ошибка при чтении файла, выводим сообщение об ошибке.
        Console.WriteLine($"Ошибка при загрузке данных: {ex.Message}");
    }

    return tweets; // Возвращаем список, содержащий загруженные твиты.
}

// Метод для извлечения хештегов из текста твита.
// Принимает текст твита 'tweet' и возвращает список хештегов.
List<string> ExtractHashtags(string tweet)
{
    List<string> hashtags = new List<string>(); // Создаем список для хранения хештегов.

    string[] parts = tweet.Split(','); // Разбиваем текст твита на части по запятой.

    if (parts.Length >= 10) // Проверяем, что в данных присутствует столбец с хештегами (нумерация с 0).
    {
        string hashtagsString = parts[9]; // Получаем строку с хештегами. Здесь указывается номер столбца.

        if (!string.IsNullOrEmpty(hashtagsString)) // Проверяем, что строка с хештегами не пустая.
        {
            string[] hashtagArray = hashtagsString.Split(','); // Разбиваем строку на отдельные хештеги.

            foreach (string hashtag in hashtagArray)
            {
                string
                    cleanedHashtag =
                        hashtag.Trim().ToLower(); // Очищаем хештег от символа '#' и приводим к нижнему регистру.

                if (!string.IsNullOrEmpty(cleanedHashtag)) // Проверяем, что хештег не пустой после очистки.
                {
                    hashtags.Add(cleanedHashtag); // Добавляем очищенный хештег в список 'hashtags'.
                }
            }
        }
    }

    return hashtags; // Возвращаем список извлеченных хештегов из твита.
}

// Метод для извлечения столицы из текста твита.
// Принимает текст твита 'tweet' и возвращает строку с названием столицы.
string ExtractCapital(string tweet)
{
    string capital = null; // Инициализируем переменную 'capital' как null.

    string[] parts = tweet.Split(','); // Разбиваем текст твита на части по запятой.

    if (parts.Length >= 14) // Проверяем, что в данных присутствует столбец с данными о столице (нумерация с 0).
    {
        capital = parts[13]; // Получаем строку с названием столицы. Здесь указывается номер столбца.

        if (!string.IsNullOrEmpty(capital)) // Проверяем, что название столицы не пустое.
        {
            capital = capital.Trim(); // Удаляем лишние пробелы из названия столицы.
        }
    }

    return capital; // Возвращаем название столицы или null, если не найдено.
}

// Метод для разделения данных на части (партиции).
// Принимает список данных 'data' и количество партиций 'numPartitions'.
// Возвращает список списков, где каждый список представляет собой партицию данных.
List<List<string?>> PartitionData(List<string?> data, int numPartitions)
{
    List<List<string?>> partitions = new List<List<string?>>(numPartitions); // Создаем список для хранения партиций.

    // Вычисляем размер каждой части данных (партиции).
    int partitionSize = data.Count / numPartitions;

    // Разделяем данные на части (партиции) в соответствии с заданным количеством партиций.
    for (int i = 0; i < numPartitions; i++)
    {
        int startIndex = i * partitionSize; // Вычисляем начальный индекс для текущей партиции.
        int endIndex = (i == numPartitions - 1) ? data.Count : startIndex + partitionSize; // Вычисляем конечный индекс.

        // Получаем данные для текущей партиции и добавляем их в список 'partitions'.
        List<string?> partition = data.GetRange(startIndex, endIndex - startIndex);
        partitions.Add(partition);
    }

    return partitions; // Возвращаем список партиций данных.
}

// Метод для получения списка 10 наиболее упоминаемых хештегов из списка твитов.
// Принимает список 'tweets', где каждый элемент представляет собой текст твита.
// Возвращает список строк с хештегами.
List<string> GetTopHashtags(List<string> tweets)
{
    Dictionary<string, int>
        hashtagFrequency = new Dictionary<string, int>(); // Создаем словарь для подсчета частоты упоминания хештегов.

    foreach (string tweet in tweets) // Проходим по каждому твиту в списке.
    {
        string[] parts = tweet.Split(','); // Разбиваем текст твита на части, предполагая разделение запятой.

        if (parts.Length >= 10) // Убедитесь, что в данных присутствует столбец с хештегами (нумерация с 0).
        {
            string hashtags = parts[9]; // Получаем строку с хештегами. Здесь указывается номер столбца.

            if (!string.IsNullOrEmpty(hashtags)) // Проверяем, что строка с хештегами не пустая.
            {
                string[] hashtagArray = hashtags.Split(','); // Разделяем хештеги по запятой.

                foreach (string hashtag in hashtagArray) // Проходим по каждому хештегу.
                {
                    string cleanedHashtag =
                        hashtag.Trim().ToLower(); // Удаляем знаки '#' и приводим хештег к нижнему регистру.

                    if (!string.IsNullOrEmpty(cleanedHashtag)) // Проверяем, что хештег не пустой после очистки.
                    {
                        if (hashtagFrequency.ContainsKey(cleanedHashtag)) // Проверяем, есть ли хештег в словаре.
                        {
                            hashtagFrequency[cleanedHashtag]++; // Увеличиваем счетчик упоминаний хештега.
                        }
                        else
                        {
                            hashtagFrequency[cleanedHashtag] = 1; // Добавляем хештег в словарь, если его нет.
                        }
                    }
                }
            }
        }
    }

    // Выбираем 10 наиболее упоминаемых хештегов, сортируя по частоте упоминаний.
    List<string> topHashtags = hashtagFrequency.OrderByDescending(pair => pair.Value)
        .Take(10)
        .Select(pair => pair.Key)
        .ToList();

    return topHashtags; // Возвращаем список 10 наиболее упоминаемых хештегов.
}

// Метод для получения списка 10 наиболее упоминаемых столиц, из которых твиты отправлялись наиболее часто.
// Принимает список 'tweets', где каждый элемент представляет собой текст твита.
// Возвращает список строк с названиями столиц.
List<string> GetTopCapitalCities(List<string?> tweets)
{
    Dictionary<string, int>
        capitalFrequency = new Dictionary<string, int>(); // Создаем словарь для подсчета частоты упоминания столиц.

    foreach (string? tweet in tweets) // Проходим по каждому твиту в списке.
    {
        string[] parts = tweet.Split(','); // Разбиваем текст твита на части, предполагая разделение запятой.

        if (parts.Length >= 14) // Убедитесь, что в данных присутствует столбец с данными о столице (нумерация с 0).
        {
            string capital = parts[13]; // Получаем строку с названием столицы. Здесь указывается номер столбца.

            if (!string.IsNullOrEmpty(capital)) // Проверяем, что строка с названием столицы не пустая.
            {
                if (capitalFrequency.ContainsKey(capital)) // Проверяем, есть ли столица в словаре.
                {
                    capitalFrequency[capital]++; // Увеличиваем счетчик упоминаний столицы.
                }
                else
                {
                    capitalFrequency[capital] = 1; // Добавляем столицу в словарь, если ее нет.
                }
            }
        }
    }

    // Выбираем 10 наиболее упоминаемых столиц, сортируя по частоте упоминаний.
    List<string> topCapitals = capitalFrequency.OrderByDescending(pair => pair.Value)
        .Take(10)
        .Select(pair => pair.Key)
        .ToList();

    return topCapitals; // Возвращаем список 10 наиболее упоминаемых столиц.
}

// Метод для сериализации объекта 'obj' в массив байтов.
// Принимает объект 'obj' и возвращает массив байтов.
static byte[] SerializeObject<T>(T obj)
{
    return
        JsonSerializer.SerializeToUtf8Bytes(obj); // Используем JsonSerializer для сериализации объекта в массив байтов.
}

// Метод для десериализации массива байтов 'arrBytes' в объект типа 'T'.
// Принимает массив байтов 'arrBytes' и возвращает десериализованный объект типа 'T'.
static T DeserializeObject<T>(byte[] arrBytes)
{
    return
        JsonSerializer
            .Deserialize<T>(arrBytes); // Используем JsonSerializer для десериализации массива байтов в объект.
}