﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Application.Dtos
{
    public class QuestionDto
    {
        public string Text { get; set; } = string.Empty;
        public int ExamId { get; set; }
    }

    public class QuestionEditDto
    {
        public string Text { get; set; } = string.Empty;
    }


}
