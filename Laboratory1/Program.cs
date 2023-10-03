using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;

// Задаем пути к изображениям
string[] imagePaths = { "image-1024x768.jpg", "image-1280x960.jpg", "image-2048x1536.jpg" };

foreach (var imagePath in imagePaths)
{
    Console.WriteLine($"Обработка изображения: {imagePath}");

    // Замеряем и выводим среднее время обработки
    double averageTime = MeasureProcessingTime(imagePath);
    Console.WriteLine($"Среднее время обработки: {averageTime} мс\n");
}

double MeasureProcessingTime(string imagePath)
{
    const int Iterations = 3;

    // Загрузка изображения
    Bitmap originalImage = new Bitmap(imagePath);

    // Создание ядра для свертки
    float[,] convolutionMatrix =
    {
        { -1, -1, -1 },
        { -1, 9, -1 },
        { -1, -1, -1 }
    };

    double totalTime = 0;

    for (int i = 0; i < Iterations; i++)
    {
        // Замеряем время выполнения
        Stopwatch stopwatch = Stopwatch.StartNew();

        // Применение свертки и уменьшение масштаба
        ProcessImage(originalImage, convolutionMatrix, "output_" + imagePath);

        stopwatch.Stop();

        // Суммируем затраченное время
        totalTime += stopwatch.ElapsedMilliseconds;
    }

    // Освобождение ресурсов
    originalImage.Dispose();

    // Возвращаем среднее время
    return totalTime / Iterations;
}

void ProcessImage(Bitmap originalImage, float[,] convolutionMatrix, string outputPath)
{
    // Применение свертки
    Bitmap convolvedImage = ApplyConvolution(originalImage, convolutionMatrix);

    // Уменьшение масштаба в два раза
    Bitmap scaledImage = ScaleImage(convolvedImage, 0.5f);

    // Сохранение результата
    scaledImage.Save(outputPath, ImageFormat.Jpeg);

    // Освобождение ресурсов
    convolvedImage.Dispose();
    scaledImage.Dispose();
}

// Применение свертки к изображению
Bitmap ApplyConvolution(Bitmap sourceImage, float[,] kernel)
{
    int width = sourceImage.Width;
    int height = sourceImage.Height;
    Bitmap resultImage = new Bitmap(width, height);

    // Проходим по каждому пикселю внутри изображения
    for (int x = 1; x < width - 1; x++)
    {
        for (int y = 1; y < height - 1; y++)
        {
            // Применяем ядро свертки к текущему пикселю
            ApplyKernel(sourceImage, x, y, kernel, out float red, out float green, out float blue);

            // Убеждаемся, что значения находятся в допустимых пределах
            red = Math.Max(0, Math.Min(255, red));
            green = Math.Max(0, Math.Min(255, green));
            blue = Math.Max(0, Math.Min(255, blue));

            // Устанавливаем полученный пиксель в результирующее изображение
            resultImage.SetPixel(x, y, Color.FromArgb((int)red, (int)green, (int)blue));
        }
    }

    return resultImage;
}

void ApplyKernel(Bitmap sourceImage, int x, int y, float[,] kernel, out float red, out float green, out float blue)
{
    red = green = blue = 0;

    // Проходим по ядру свертки
    for (int i = -1; i <= 1; i++)
    {
        for (int j = -1; j <= 1; j++)
        {
            Color pixel = sourceImage.GetPixel(x + i, y + j);
            // Выполняем свертку для каждого цветового канала (R, G, B)
            red += pixel.R * kernel[i + 1, j + 1];
            green += pixel.G * kernel[i + 1, j + 1];
            blue += pixel.B * kernel[i + 1, j + 1];
        }
    }
}

// Уменьшение масштаба изображения
Bitmap ScaleImage(Bitmap sourceImage, float scaleFactor)
{
    int newWidth = (int)(sourceImage.Width * scaleFactor);
    int newHeight = (int)(sourceImage.Height * scaleFactor);
    Bitmap resultImage = new Bitmap(newWidth, newHeight);

    // Проходим по каждому пикселю в результирующем изображении
    for (int x = 0; x < newWidth; x++)
    {
        for (int y = 0; y < newHeight; y++)
        {
            // Находим соответствующий пиксель в исходном изображении
            int srcX = (int)(x / scaleFactor);
            int srcY = (int)(y / scaleFactor);
            Color pixel = sourceImage.GetPixel(srcX, srcY);
            // Устанавливаем полученный пиксель в результирующее изображение
            resultImage.SetPixel(x, y, pixel);
        }
    }

    return resultImage;
}