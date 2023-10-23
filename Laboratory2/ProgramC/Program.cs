using System.Diagnostics;
using System.Globalization;
using Accord.MachineLearning;
using CsvHelper;
using CsvHelper.Configuration;

object lockObject = new object();
string filePath = "BD-Patients.csv";

// Загрузка данных с использованием CsvHelper
var data = LoadCsvData(filePath);

// Извлечение столбцов "Creatinine_pvariance" и "HCO3_mean"
double[][] features = data.Select(d => new[] { d.Creatinine_pvariance, d.HCO3_mean }).ToArray();

int[] kValues = { 3, 4, 5 };
int[] vectorCounts = { 1000, 3000, 5000 };
int[] threadCounts = { 2, 4, 6, 8, 10, 12, 14, 16 };

KMeansClusterCollection clusters = null;

foreach (int k in kValues)
{
    foreach (int vectorCount in vectorCounts)
    {
        Console.WriteLine($"Clustering for K={k} and {vectorCount} vectors");

        // Случайная подвыборка данных до указанного vectorCount
        Random rand = new Random();
        int[] subsampleIndices = Enumerable.Range(0, data.Length).OrderBy(x => rand.Next()).Take(vectorCount).ToArray();
        double[][] subsample = subsampleIndices.Select(i => features[i]).ToArray();

        double silhouette = 0;

        foreach (int threadCount in threadCounts)
        {
            Console.WriteLine($"Using {threadCount} threads:");

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            // Выполнение кластеризации с помощью K-means
            KMeans kmeans = new KMeans(k);
            clusters = kmeans.Learn(subsample);

            // Вычисление индекса Silhouette с использованием нескольких потоков
            silhouette = CalculateSilhouetteParallel(subsample, clusters, threadCount);

            stopwatch.Stop();

            Console.WriteLine($"Silhouette Index: {silhouette}");
            Console.WriteLine($"Elapsed Time: {stopwatch.Elapsed.TotalMilliseconds} ms");
        }

        // Вывод центров кластеров
        for (int clusterIndex = 0; clusterIndex < k; clusterIndex++)
        {
            double[] center = clusters.Centroids[clusterIndex];
            Console.Write($"Cluster {clusterIndex} Center: [");
            for (int dim = 0; dim < center.Length; dim++)
            {
                Console.Write($"{center[dim]}");
                if (dim < center.Length - 1)
                {
                    Console.Write(", ");
                }
            }

            Console.WriteLine("]");
        }
    }
}

// Load data from a CSV file using CsvHelper
Data[] LoadCsvData(string filePath)
{
    using (var reader = new StreamReader(filePath))
    using (var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)))
    {
        csv.Context.RegisterClassMap<CustomDataMap>();
        var records = csv.GetRecords<Data>().ToList();
        return records.ToArray();
    }
}

// Calculate Silhouette index for clustering
double CalculateSilhouetteParallel(double[][] data, KMeansClusterCollection clusters, int threadCount)
{
    double silhouette = 0;

    Parallel.For(0, data.Length, new ParallelOptions { MaxDegreeOfParallelism = threadCount }, i =>
    {
        int clusterIndex = clusters.Decide(data[i]);
        double a = CalculateA(data[i], clusterIndex, clusters);

        double b = CalculateB(data[i], clusterIndex, clusters);

        lock (lockObject)
        {
            silhouette += (b - a) / Math.Max(a, b);
        }
    });

    return silhouette / data.Length;
}

double CalculateA(double[] dataPoint, int clusterIndex, KMeansClusterCollection clusters)
{
    double a = 0;
    int clusterSize = clusters.Count;

    for (int i = 0; i < clusterSize; i++)
    {
        if (i != clusterIndex)
        {
            a += CalculateDistance(dataPoint, clusters[clusterIndex].Centroid) / (clusterSize - 1);
        }
    }

    return a;
}

double CalculateB(double[] dataPoint, int clusterIndex, KMeansClusterCollection clusters)
{
    double b = double.MaxValue;

    for (int j = 0; j < clusters.Count; j++)
    {
        if (j != clusterIndex)
        {
            double currentB = CalculateDistance(dataPoint, clusters[j].Centroid);
            b = Math.Min(b, currentB);
        }
    }

    return b;
}

double CalculateDistance(double[] a, double[] b)
{
    double distance = 0;
    for (int i = 0; i < a.Length; i++)
    {
        distance += (a[i] - b[i]) * (a[i] - b[i]);
    }

    return Math.Sqrt(distance);
}

public class Data
{
    public double Creatinine_pvariance { get; set; }
    public double HCO3_mean { get; set; }
}

public class CustomDataMap : ClassMap<Data>
{
    public CustomDataMap()
    {
        Map(m => m.Creatinine_pvariance).Name("Creatinine_pvariance").Default(0.0);
        Map(m => m.HCO3_mean).Name("HCO3_mean").Default(0.0);
    }
}