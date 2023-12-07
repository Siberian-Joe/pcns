using System.Diagnostics;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;

string inputImagePath = "10240x7680.jpg"; // Путь к исходному изображению
string outputImagePath = $"output - {inputImagePath}"; // Путь для сохранения обработанного изображения

int numberOfRuns = 3; // Количество повторений процедуры для замера времени
long totalMilliseconds = 0; // Суммарное время выполнения всех повторений

// Выполнение процедуры обработки изображения несколько раз для замера среднего времени
for (int i = 0; i < numberOfRuns; i++)
{
    Stopwatch stopwatch = Stopwatch.StartNew(); // Запуск таймера

    // Вызов функции обработки изображения
    ProcessImage(inputImagePath, outputImagePath);

    stopwatch.Stop(); // Остановка таймера
    totalMilliseconds += stopwatch.ElapsedMilliseconds; // Добавление времени выполнения к общему времени
}

double averageMilliseconds = totalMilliseconds / (double)numberOfRuns; // Расчёт среднего времени выполнения
Console.WriteLine($"Среднее время выполнения: {averageMilliseconds} мс"); // Вывод среднего времени

void ProcessImage(string inputPath, string outputPath)
{
    // Загрузка изображения
    Image<Bgr, byte> image = new Image<Bgr, byte>(inputPath);

    // Преобразование изображения в градации серого
    Image<Gray, byte> grayImage = image.Convert<Gray, byte>();

    // Применение фильтра Собеля для выделения горизонтальных и вертикальных границ
    Mat sobelX = new Mat();
    Mat sobelY = new Mat();
    CvInvoke.Sobel(grayImage, sobelX, DepthType.Cv8U, 1, 0);
    CvInvoke.Sobel(grayImage, sobelY, DepthType.Cv8U, 0, 1);

    // Комбинирование результатов фильтров Собеля
    Mat sobel = new Mat();
    CvInvoke.Add(sobelX, sobelY, sobel);
    CvInvoke.ConvertScaleAbs(sobel, sobel, 1, 0);

    // Конвертация результата обратно в изображение
    Image<Gray, byte> sobelImage = sobel.ToImage<Gray, byte>();

    // Уменьшение размера изображения в два раза
    Image<Gray, byte> resizedImage = sobelImage.Resize(0.5, Inter.Linear);

    // Сохранение обработанного изображения
    resizedImage.ToBitmap().Save(outputPath);
}