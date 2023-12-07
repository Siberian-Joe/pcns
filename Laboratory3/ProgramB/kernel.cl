__kernel void combineImages(__global uchar* original, __global uchar* half, __global uchar* quarter, __global uchar* output, int width, int height) {
    int x = get_global_id(0);
    int y = get_global_id(1);

    if (x < width && y < height) {
        // Пример вычисления позиции в массиве
        int index = y * width + x;

        // Объединение пикселей из разных изображений
        // Это просто пример, на практике логика может быть другой
        uchar pixelOriginal = original[index];
        uchar pixelHalf = half[index / 2]; // Предполагается, что размер уменьшен в два раза
        uchar pixelQuarter = quarter[index / 4]; // Предполагается, что размер уменьшен в четыре раза

        // Простая логика объединения - среднее значение
        output[index] = (pixelOriginal + pixelHalf + pixelQuarter) / 3;
    }
}
