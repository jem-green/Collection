using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Collection
{
    class Program
    {
        static void Main(string[] args)
        {          
            Collection<string> c = new Collection<string>();

            c.Add("start");
            c.Add("next");
            c.Add("end");
            Console.WriteLine("count=" + c.Count);

            Console.WriteLine("data[0]=" + c[0]);
            Console.WriteLine("data[1]=" + c[1]);
            Console.WriteLine("data[2]=" + c[2]);

            for (int i = 0; i < c.Count; i++)
            {
                Console.WriteLine("Iterate " + c[i]);
            }

            Console.WriteLine("Clear Collection");
            c.Clear();
            c.Add("end");
            Console.WriteLine("data[0]=" + c[0]);
            c.Add("next");
            c.Add("start");

            Console.WriteLine("Update Collection");
            //d[1] = "to";
            c[1] = "between";

            Console.WriteLine("count=" + c.Count);
            Console.WriteLine("data[0]=" + c[0]);
            Console.WriteLine("data[1]=" + c[1]);
            Console.WriteLine("data[2]=" + c[2]);

            //Console.WriteLine("Remove[\"between\"]=" + c.Remove("between"));
            //Console.WriteLine("count=" + c.Count);
            //Console.WriteLine("data[0]=" + c[0]);
            //Console.WriteLine("data[1]=" + c[1]);

            c.RemoveAt(2);
            Console.WriteLine("RemoveAt[2]");
            foreach (string item in c)
            {
                Console.WriteLine("Enumerate " + item);
            }

            Console.WriteLine("contains[\"start\"]=" + c.Contains("start"));

            Console.WriteLine("index of [\"start\"]=" + c.IndexOf("start"));

            Console.WriteLine("--------");
            PersistentCollection<string> pc = new PersistentCollection<string>(true);

            pc.Add("start");
            pc.Add("next");
            pc.Add("end");
            Console.WriteLine("count=" + pc.Count);

            Console.WriteLine("data[0]=" + pc[0]);
            Console.WriteLine("data[1]=" + pc[1]);
            Console.WriteLine("data[2]=" + pc[2]);

            for (int i = 0; i < c.Count; i++)
            {
                Console.WriteLine("Iterate " + c[i]);
            }

            Console.WriteLine("Clear Collection");
            pc.Clear();
            pc.Add("end");
            Console.WriteLine("data[0]=" + pc[0]);
            pc.Add("next");
            pc.Add("start");

            Console.WriteLine("Update Collection");
            //d[1] = "to";
            pc[1] = "between";

            Console.WriteLine("count=" + pc.Count);
            Console.WriteLine("data[0]=" + pc[0]);
            Console.WriteLine("data[1]=" + pc[1]);
            Console.WriteLine("data[2]=" + pc[2]);

            //Console.WriteLine("Remove[\"between\"]=" + pc.Remove("between"));
            //Console.WriteLine("count=" + pc.Count);
            //Console.WriteLine("data[0]=" + pc[0]);
            //Console.WriteLine("data[1]=" + pc[1]);

            pc.RemoveAt(2);
            Console.WriteLine("RemoveAt[2]");

            foreach (string item in pc)
            {
                Console.WriteLine("Enumerate " + item);
            }

            Console.WriteLine("contains[\"start\"]=" + pc.Contains("start"));

            Console.WriteLine("index of [\"start\"]=" + pc.IndexOf("start"));
        }
    }
}
