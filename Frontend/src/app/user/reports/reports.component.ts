import { Component, OnInit } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { AuthenticationService } from './../../services/auth.service';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-reports',
  templateUrl: './reports.component.html',
  styleUrls: ['./reports.component.css'],
  standalone: true,
  imports: [CommonModule,FormsModule],
})
export class ReportsComponent implements OnInit {
  examResults: any[] = [];
  errorMessage: string = '';
  role: string | null = null;
  pageNumber: number = 1;
  pageSize: number = 10; // Default page size
  totalCount: number = 0;
  totalPages: number = 0;
  userId = '';

  constructor(
    private http: HttpClient,
    private authService: AuthenticationService
  ) {}

  ngOnInit(): void {
    this.userId = this.authService.getUserId()!;
    this.getExamResults();
  }

  getExamResults(): void {
    const params = new HttpParams()
      .set('pageNumber', this.pageNumber.toString())
      .set('pageSize', this.pageSize.toString());

    this.http
      .get<any>(`http://localhost:5063/api/Exam/history/${this.userId}`, {
        params,
      })
      .subscribe({
        next: (data) => {
          this.examResults = data?.data;
          this.totalCount = data?.totalCount;
          this.totalPages = Math.ceil(this.totalCount / this.pageSize);
        },
        error: (err) => {
          console.error('Failed to fetch exam results', err);
          this.errorMessage =
            'Could not fetch exam results. Please try again later.';
        },
      });
  }

  previousPage(): void {
    if (this.pageNumber > 1) {
      this.pageNumber--;
      this.getExamResults();
    }
  }

  nextPage(): void {
    if (this.pageNumber < this.totalPages) {
      this.pageNumber++;
      this.getExamResults();
    }
  }

  onPageSizeChange(): void {
    this.pageNumber = 1; // Reset to the first page whenever the page size changes
    this.getExamResults(); // Fetch data with the new page size
  }
}
