export interface SalaryProfile {
  baseMonthlySalary: number
  netMonthlySalary: number
  providentMonthly: number
  bonusMonths: number
  bonusNetAmount: number
  bonusPayoutMonth: number
  payDay: number
}

export interface SalaryProfileOverview {
  profile: SalaryProfile | null
}

export interface SaveSalaryProfileInput extends SalaryProfile {
  requestId: string
}
