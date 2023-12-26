using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using MPI;
using Environment = MPI.Environment;

try
{
    // Инициализация MPI окружения
    using (new Environment(ref args))
    {
        // Получение коммуникатора MPI для всех процессов
        var comm = Communicator.world;
        
        // Создание объекта для измерения времени выполнения
        var stopwatch = Stopwatch.StartNew();

        // Задаем массив URL-адресов для анализа
        string[] urls =
        {
            "https://ru.wikipedia.org/wiki/Apple",
            "https://ru.wikipedia.org/wiki/Microsoft",
            "https://ru.wikipedia.org/wiki/Google_(компания)",
            "https://ru.wikipedia.org/wiki/Samsung",
            "https://ru.wikipedia.org/wiki/Nvidia",
            "https://ru.wikipedia.org/wiki/SpaceX"
        };

        // Распределение URL-адресов по процессам
        var urlsPerProcess = urls.Length / comm.Size;
        var assignedUrls = DistributeUrls(urls, comm.Rank, urlsPerProcess);

        // Инициализация словаря для сбора результатов
        var allResults = new Dictionary<string, int>();

        // Перебор URL-адресов, назначенных данному процессу
        foreach (var url in assignedUrls)
        {
            // Получение содержимого веб-страницы
            var content = FetchWebContent(url);
            
            // Извлечение годов из содержимого
            var years = ExtractYears(content);
            
            // Нахождение года с наибольшим количеством событий
            var mostEventsYear = FindYearWithMostEvents(years);

            // Слияние результатов с общими результатами
            foreach (var kvp in mostEventsYear)
            {
                var key = kvp.Key;
                var value = kvp.Value;

                if (allResults.TryAdd(key, value) == false)
                    allResults[key] += value;
            }
        }

        // Отправка результатов на корневой процесс
        if (comm.Rank != 0)
        {
            comm.Send(SerializeObject(allResults), 0, 0);
        }

        // Остановка счетчика времени выполнения
        stopwatch.Stop();
        var processTime = stopwatch.ElapsedMilliseconds;

        // Сбор и агрегация результатов на корневом процессе
        if (comm.Rank == 0)
        {
            for (int i = 1; i < comm.Size; i++)
            {
                // Получение результатов от других процессов
                var result = DeserializeObject<Dictionary<string, int>>(comm.Receive<byte[]>(i, 0));
                // Агрегация результатов
                foreach (var kvp in result)
                {
                    if (allResults.ContainsKey(kvp.Key))
                        allResults[kvp.Key] += kvp.Value;
                    else
                        allResults[kvp.Key] = kvp.Value;
                }
            }

            // Вывод агрегированных результатов
            Console.WriteLine($"Общее время выполнения всех процессов: {processTime} мс");
            // Отображение окончательных результатов
        }
        else
        {
            // Вывод времени выполнения для остальных процессов
            Console.WriteLine($"Время выполнения процесса {comm.Rank}: {processTime} мс");
        }
    }
}
catch (Exception ex)
{
    // Обработка и вывод ошибки, если что-то пошло не так
    Console.WriteLine($"Ошибка при выполнении программы: {ex.Message}");
}

// Функция DistributeUrls принимает массив URL-адресов, номер текущего процесса (rank) и количество URL-адресов,
// которые должны быть назначены этому процессу. Она определяет, какие URL-адреса должны быть назначены текущему
// процессу и возвращает их в виде массива строк.
string[] DistributeUrls(string[] urls, int rank, int urlsPerProcess)
{
    // Общее количество URL-адресов
    int totalUrls = urls.Length;
    // Начальный индекс для назначения URL-адресов текущему процессу
    int start = rank * urlsPerProcess;
    // Конечный индекс для назначения URL-адресов текущему процессу
    int end = (rank + 1) * urlsPerProcess;

    // Проверяем, есть ли URL-адреса для распределения на текущем процессе
    if (start >= totalUrls) return new string[0];

    // Если текущий процесс последний, то он получает все оставшиеся URL-адреса
    if (rank == Communicator.world.Size - 1)
    {
        end = totalUrls;
    }

    // Создаем список для хранения назначенных URL-адресов
    List<string> assignedUrls = new List<string>();
    // Проходим по URL-адресам и добавляем их в список назначенных
    for (int i = start; i < end && i < totalUrls; i++)
    {
        assignedUrls.Add(urls[i]);
    }

    // Возвращаем назначенные URL-адреса в виде массива строк
    return assignedUrls.ToArray();
}

// Функция FetchWebContent принимает URL-адрес и пытается получить содержимое веб-страницы с использованием HTTP-запроса.
// Если запрос успешен, она возвращает содержимое в виде строки. В противном случае, возвращается пустая строка.
string FetchWebContent(string url)
{
    try
    {
        using var client = new HttpClient();
        using var response = client.GetAsync(url).Result;
        // Если запрос успешен, считываем содержимое и возвращаем его
        if (response.IsSuccessStatusCode)
            return response.Content.ReadAsStringAsync().Result;
    }
    catch (Exception ex)
    {
        // В случае ошибки выводим сообщение об ошибке и возвращаем пустую строку
        Console.WriteLine($"Ошибка при получении содержимого веб-страницы: {ex.Message}");
    }

    return string.Empty;
}

// Функция ExtractYears принимает текст веб-страницы и извлекает из него годы, используя регулярное выражение.
// Она возвращает словарь, где ключи - это годы, а значения - количество раз, которое каждый год встречается на странице.
Dictionary<string, int> ExtractYears(string content)
{
    var yearOccurrences = new Dictionary<string, int>();
    // Регулярное выражение для поиска годов
    var yearRegex = new Regex(@"\b(1\d{3}|20[0-1]\d|2100)\b");

    foreach (Match match in yearRegex.Matches(content))
    {
        var year = match.Value;
        if (yearOccurrences.TryAdd(year, 1) == false)
        {
            yearOccurrences[year]++;
        }
    }

    return yearOccurrences;
}

// Функция FindYearWithMostEvents принимает словарь с годами и их количеством и находит год с наибольшим количеством событий.
// Она возвращает словарь с одним элементом, где ключ - это год с наибольшим количеством событий, а значение - это это количество.
Dictionary<string, int> FindYearWithMostEvents(Dictionary<string, int> yearOccurrences)
{
    var yearWithMostEvents = string.Empty;
    var maxOccurrences = 0;

    foreach (var pair in yearOccurrences)
    {
        if (pair.Value > maxOccurrences)
        {
            maxOccurrences = pair.Value;
            yearWithMostEvents = pair.Key;
        }
    }

    var result = new Dictionary<string, int>();
    result.Add(yearWithMostEvents, maxOccurrences);

    return result;
}

// Функция SerializeObject принимает объект и сериализует его в массив байтов с использованием JSON-сериализации.
byte[] SerializeObject<T>(T obj)
{
    return JsonSerializer.SerializeToUtf8Bytes(obj);
}

// Функция DeserializeObject принимает массив байтов и десериализует его в объект с использованием JSON-десериализации.
T? DeserializeObject<T>(byte[] arrBytes)
{
    return JsonSerializer.Deserialize<T>(arrBytes);
}