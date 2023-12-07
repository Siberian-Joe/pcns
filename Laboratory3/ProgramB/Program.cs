using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Cloo; // Подключаем библиотеку OpenCL

// Пути к изображениям
string originalImagePath = "10240x7680.jpg";
string halfSizeImagePath = "5120x3840.jpg";
string quarterSizeImagePath = "2560x1920.jpg";
string outputImagePath = $"output - {originalImagePath}";

// Загрузка и обработка изображений
byte[] originalImageData = LoadImage(originalImagePath);
byte[] halfSizeImageData = LoadImage(halfSizeImagePath);
byte[] quarterSizeImageData = LoadImage(quarterSizeImagePath);

// Предполагаемые размеры изображений (примерные значения)
int width = 10240; // ширина изображения
int height = 7680; // высота изображения

// Обработка изображений с помощью OpenCL
byte[] outputImageData =
    ProcessImagesWithOpenCL(originalImageData, halfSizeImageData, quarterSizeImageData, width, height);

// Сохранение результата в файл
SaveImage(outputImageData, outputImagePath, width, height, PixelFormat.Format24bppRgb);

static byte[] LoadImage(string path)
{
    // Загрузка изображения из файла по указанному пути
    using (Bitmap image = new Bitmap(path))
    {
        // Определение прямоугольника для блокировки битов изображения
        var rect = new Rectangle(0, 0, image.Width, image.Height);
        // Блокировка битов изображения для чтения
        var bitmapData = image.LockBits(rect, ImageLockMode.ReadOnly, image.PixelFormat);

        // Определение количества байтов на пиксель
        int bytesPerPixel = Bitmap.GetPixelFormatSize(image.PixelFormat) / 8;
        // Вычисление общего количества байтов в изображении
        int byteCount = bitmapData.Stride * image.Height;
        // Создание массива для хранения байтов изображения
        byte[] pixels = new byte[byteCount];

        // Получение указателя на первый байт массива битов изображения
        IntPtr ptr = bitmapData.Scan0;
        // Копирование байтов изображения в массив
        Marshal.Copy(ptr, pixels, 0, pixels.Length);

        // Разблокирование битов изображения
        image.UnlockBits(bitmapData);

        // Возвращение массива байтов
        return pixels;
    }
}

byte[] ProcessImagesWithOpenCL(byte[] original, byte[] half, byte[] quarter, int width, int height)
{
    // Создание выходного массива для хранения результата обработки
    byte[] output = new byte[original.Length];

    // Выбор первой доступной OpenCL платформы и устройства
    ComputePlatform platform = ComputePlatform.Platforms[0];
    ComputeDevice device = platform.Devices[0];

    // Создание контекста OpenCL для работы с выбранным устройством
    using (ComputeContext context = new ComputeContext(new[] { device }, new ComputeContextPropertyList(platform), null, IntPtr.Zero))
    {
        // Создание очереди команд для выполнения операций на устройстве
        using (ComputeCommandQueue queue = new ComputeCommandQueue(context, device, ComputeCommandQueueFlags.None))
        {
            // Создание буферов для хранения данных изображений в памяти устройства
            using (ComputeBuffer<byte> originalBuffer = new ComputeBuffer<byte>(context, ComputeMemoryFlags.ReadOnly | ComputeMemoryFlags.CopyHostPointer, original))
            using (ComputeBuffer<byte> halfBuffer = new ComputeBuffer<byte>(context, ComputeMemoryFlags.ReadOnly | ComputeMemoryFlags.CopyHostPointer, half))
            using (ComputeBuffer<byte> quarterBuffer = new ComputeBuffer<byte>(context, ComputeMemoryFlags.ReadOnly | ComputeMemoryFlags.CopyHostPointer, quarter))
            using (ComputeBuffer<byte> outputBuffer = new ComputeBuffer<byte>(context, ComputeMemoryFlags.WriteOnly, output.Length))
            {
                // Чтение исходного кода кернела из файла
                string kernelSource = File.ReadAllText("kernel.cl");

                // Создание программы OpenCL из исходного кода кернела
                using (ComputeProgram program = new ComputeProgram(context, kernelSource))
                {
                    try
                    {
                        // Компиляция программы для устройства
                        program.Build(null, null, null, IntPtr.Zero);

                        // Создание кернела из скомпилированной программы
                        using (ComputeKernel kernel = program.CreateKernel("combineImages"))
                        {
                            // Установка аргументов для кернела
                            kernel.SetMemoryArgument(0, originalBuffer);
                            kernel.SetMemoryArgument(1, halfBuffer);
                            kernel.SetMemoryArgument(2, quarterBuffer);
                            kernel.SetMemoryArgument(3, outputBuffer);
                            kernel.SetValueArgument(4, width);
                            kernel.SetValueArgument(5, height);

                            // Запуск кернела на устройстве
                            queue.Execute(kernel, null, new long[] { width, height }, null, null);

                            // Чтение результата обработки из памяти устройства обратно в оперативную память
                            queue.ReadFromBuffer(outputBuffer, ref output, true, null);
                        }
                    }
                    catch (ComputeException e)
                    {
                        // Вывод информации об ошибке в случае неудачной компиляции программы
                        Console.WriteLine($"Ошибка при компиляции OpenCL программы: {e.Message}");
                    }
                }
            }
        }
    }

    return output; // Возвращение результата обработки
}

void SaveImage(byte[] imageData, string path, int width, int height, PixelFormat pixelFormat)
{
    using (Bitmap image = new Bitmap(width, height, pixelFormat))
    {
        Rectangle rect = new Rectangle(0, 0, width, height);
        BitmapData bitmapData = image.LockBits(rect, ImageLockMode.WriteOnly, pixelFormat);

        // Получаем адрес первого элемента в битовом массиве изображения
        IntPtr ptr = bitmapData.Scan0;

        // Копируем байты изображения в битовый массив изображения
        Marshal.Copy(imageData, 0, ptr, imageData.Length);

        // Разблокируем биты изображения
        image.UnlockBits(bitmapData);

        // Сохраняем изображение
        image.Save(path);
    }
}