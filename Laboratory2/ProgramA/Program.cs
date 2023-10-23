using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using AForge.Imaging.Filters;

// Пути к изображениям
string[] imagePaths = { "2560x1920-image.jpg", "3200x2400-image.jpg", "5120x3840-image.jpg" };

// Количество потоков для параллельной обработки
int[] threadCounts = { 2, 4, 6, 8, 10, 12, 14, 16 };

// Проходим по всем значениям числа потоков
foreach (var threadCount in threadCounts)
{
    // Параллельно обрабатываем каждое изображение
    Parallel.ForEach(imagePaths, imagePath =>
    {
        Console.WriteLine($"Обработка изображения: {imagePath} с использованием {threadCount} потоков");

        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();

        // Загружаем входное изображение
        Bitmap inputImage = new Bitmap(imagePath);

        // Применяем фильтр свёртки для создания рельефного эффекта
        int[,] matrix =
        {
            { -2, -1, 0 },
            { -1, 1, 1 },
            { 0, 1, 2 }
        };
        Convolution convolutionFilter = new Convolution(matrix);
        convolutionFilter.ApplyInPlace(inputImage);

        // Уменьшаем масштаб изображения в два раза
        ResizeBilinear resizeFilter = new ResizeBilinear(inputImage.Width / 2, inputImage.Height / 2);
        inputImage = resizeFilter.Apply(inputImage);

        // Сохраняем результат
        string outputPath = $"{threadCount}_threads_processed_{imagePath}";
        inputImage.Save(outputPath, ImageFormat.Jpeg);

        stopwatch.Stop();
        Console.WriteLine(
            $"Обработка завершена для изображения {imagePath} с использованием {threadCount} потоков. Результат сохранён в {outputPath}. Затраченное время: {stopwatch.ElapsedMilliseconds} мс");
    });
}