using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using AForge.Imaging.Filters;
using Image = AForge.Imaging.Image;

// Пути к изображениям, которые будут обработаны
string[] imagePaths = { "2560x1920-image.jpg", "3200x2400-image.jpg", "5120x3840-image.jpg" };

// Количество потоков для параллельной обработки
int[] threadCounts = { 2, 4, 6, 8, 10, 12, 14, 16 };

foreach (var threadCount in threadCounts)
{
    // Параллельно обрабатываем каждое изображение
    Parallel.ForEach(imagePaths, imagePath =>
    {
        Console.WriteLine($"Обработка изображения: {imagePath} с использованием {threadCount} потоков");

        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();

        // Загрузка изображения из файла
        Bitmap inputImage = new Bitmap(imagePath);

        // Преобразование изображения в оттенки серого
        Grayscale grayscaleFilter = new Grayscale(0.2125, 0.7154, 0.0721);
        Bitmap grayscaleImage = grayscaleFilter.Apply(inputImage);

        // Применение пороговой фильтрации для создания двоичного изображения
        byte threshold = 150; // Можно настроить порог
        Threshold thresholdFilter = new Threshold(threshold);
        thresholdFilter.ApplyInPlace(grayscaleImage);

        // Применение бинарной эрозии с указанным радиусом (1, 2 или 3)
        int erosionRadius = 2; // Можно изменить этот параметр
        Bitmap binaryErosionImage = BinaryErosion(grayscaleImage, erosionRadius);

        // Преобразование двоичного изображения в цветное (0 - черный, 1 - белый)
        Bitmap binaryImage = Image.Clone(binaryErosionImage, PixelFormat.Format24bppRgb);

        stopwatch.Stop();
        Console.WriteLine(
            $"Обработка завершена для {imagePath} с использованием {threadCount} потоков. Затраченное время: {stopwatch.ElapsedMilliseconds} мс");

        // Сохранение результата
        string outputPath = $"{threadCount}_threads_processed_{imagePath}";
        binaryImage.Save(outputPath, ImageFormat.Jpeg);
    });
}

Bitmap BinaryErosion(Bitmap inputImage, int radius)
{
    Bitmap resultImage = new Bitmap(inputImage.Width, inputImage.Height);

    for (int x = 0; x < inputImage.Width; x++)
    {
        for (int y = 0; y < inputImage.Height; y++)
        {
            bool erode = true;

            for (int i = -radius; i <= radius; i++)
            {
                for (int j = -radius; j <= radius; j++)
                {
                    int newX = x + i;
                    int newY = y + j;

                    if (newX >= 0 && newX < inputImage.Width && newY >= 0 && newY < inputImage.Height)
                    {
                        if (inputImage.GetPixel(newX, newY).R == 0)
                        {
                            erode = false;
                            break;
                        }
                    }
                }

                if (!erode) break;
            }

            resultImage.SetPixel(x, y, erode ? Color.White : Color.Black);
        }
    }

    return resultImage;
}