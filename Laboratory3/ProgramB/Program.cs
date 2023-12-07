using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Cloo;

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

byte[] LoadImage(string path)
{
    using (Bitmap image = new Bitmap(path))
    {
        var rect = new Rectangle(0, 0, image.Width, image.Height);
        var bitmapData = image.LockBits(rect, ImageLockMode.ReadOnly, image.PixelFormat);

        int bytesPerPixel = Bitmap.GetPixelFormatSize(image.PixelFormat) / 8;
        int byteCount = bitmapData.Stride * image.Height;
        byte[] pixels = new byte[byteCount];

        IntPtr ptr = bitmapData.Scan0;
        Marshal.Copy(ptr, pixels, 0, pixels.Length);

        image.UnlockBits(bitmapData);
        return pixels;
    }
}


byte[] ProcessImagesWithOpenCL(byte[] original, byte[] half, byte[] quarter, int width, int height)
{
    byte[]
        output = new byte[original
            .Length]; // Предполагаем, что размер выходного массива такой же, как и у оригинального изображения

    // Создание платформы и устройства
    ComputePlatform platform = ComputePlatform.Platforms[0];
    ComputeDevice device = platform.Devices[0];

    // Создание контекста и очереди команд
    using (ComputeContext context =
           new ComputeContext(new[] { device }, new ComputeContextPropertyList(platform), null, IntPtr.Zero))
    using (ComputeCommandQueue queue = new ComputeCommandQueue(context, device, ComputeCommandQueueFlags.None))
    {
        // Создание буферов
        using (ComputeBuffer<byte> originalBuffer = new ComputeBuffer<byte>(context,
                   ComputeMemoryFlags.ReadOnly | ComputeMemoryFlags.CopyHostPointer, original))
        using (ComputeBuffer<byte> halfBuffer = new ComputeBuffer<byte>(context,
                   ComputeMemoryFlags.ReadOnly | ComputeMemoryFlags.CopyHostPointer, half))
        using (ComputeBuffer<byte> quarterBuffer = new ComputeBuffer<byte>(context,
                   ComputeMemoryFlags.ReadOnly | ComputeMemoryFlags.CopyHostPointer, quarter))
        using (ComputeBuffer<byte> outputBuffer =
               new ComputeBuffer<byte>(context, ComputeMemoryFlags.WriteOnly, output.Length))
        {
            // Загрузка и компиляция OpenCL кернела
            string kernelSource = File.ReadAllText("kernel.cl");
            using (ComputeProgram program = new ComputeProgram(context, kernelSource))
            {
                try
                {
                    try
                    {
                        program.Build(null, null, null, IntPtr.Zero);
                    }
                    catch (ComputeException ce)
                    {
                        Console.WriteLine("OpenCL program build failed with error code: " + ce.ComputeErrorCode);
                        Console.WriteLine("Build log: " + program.GetBuildLog(device));
                        throw;
                    }
                    using (ComputeKernel kernel = program.CreateKernel("combineImages"))
                    {
                        // Установка аргументов кернела
                        kernel.SetMemoryArgument(0, originalBuffer);
                        kernel.SetMemoryArgument(1, halfBuffer);
                        kernel.SetMemoryArgument(2, quarterBuffer);
                        kernel.SetMemoryArgument(3, outputBuffer);
                        kernel.SetValueArgument(4, width);
                        kernel.SetValueArgument(5, height);

                        // Выполнение кернела
                        queue.Execute(kernel, null, new long[] { width, height }, null, null);

                        // Чтение результатов
                        queue.ReadFromBuffer(outputBuffer, ref output, true, null);
                    }
                }
                catch (ComputeException e)
                {
                    Console.WriteLine($"Ошибка при компиляции OpenCL программы: {e.Message}");
                }
            }
        }
    }

    return output;
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