using System;
using System.IO;

public class TestClick {
    public static void Log(string msg) {
        File.AppendAllText("ui_test.log", msg + "\n");
    }
}
