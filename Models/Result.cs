﻿namespace zChecklist.Models
{
    public class Result<T>
    {
        public bool Success { get; set; }
        public T? Model { get; set; }
        public string? Message { get; set; }
    }
}
