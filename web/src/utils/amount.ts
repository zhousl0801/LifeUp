import Decimal from 'decimal.js'

export const MaximumAmount = 1_000_000_000

export type AmountKey =
  | '0'
  | '1'
  | '2'
  | '3'
  | '4'
  | '5'
  | '6'
  | '7'
  | '8'
  | '9'
  | '.'
  | '+'
  | '-'
  | '*'
  | '/'
  | '00'
  | '='
  | 'back'

export function applyAmountKey(expression: string, key: AmountKey): string {
  if (key === 'back') {
    return expression.slice(0, -1)
  }

  if (key === '=') {
    return evaluateAmountExpression(expression)?.toString() ?? expression
  }

  if (key === '00') {
    return applyAmountKey(applyAmountKey(expression, '0'), '0')
  }

  if (key === '+' || key === '-' || key === '*' || key === '/') {
    if (!expression) {
      return expression
    }

    let next = expression

    if (/[+*/-]$/.test(next)) {
      next = next.slice(0, -1)
    }

    if (next.endsWith('.')) {
      next = next.slice(0, -1)
    }

    return next ? `${next}${key}` : ''
  }

  if (key === '.') {
    const currentTerm = expression.split(/[+*/-]/).pop() ?? ''

    if (currentTerm.includes('.')) {
      return expression
    }

    return `${expression}${currentTerm ? '' : '0'}.`
  }

  const currentTerm = expression.split(/[+*/-]/).pop() ?? ''
  const decimalPart = currentTerm.split('.')[1]

  if (currentTerm.includes('.') && (decimalPart?.length ?? 0) >= 2) {
    return expression
  }

  let next = currentTerm === '0' ? expression.slice(0, -1) : expression

  if (next.length >= 24) {
    return expression
  }

  return `${next}${key}`
}

export function evaluateAmountExpression(expression: string): number | null {
  const normalized = expression.replace(/[+*/.-]$/, '')

  if (!/^\d+(?:\.\d{1,2})?(?:[+*/-]\d+(?:\.\d{1,2})?)*$/.test(normalized)) {
    return null
  }

  const tokens = normalized.split(/([+*/-])/)
  let total = new Decimal(0)
  let term = new Decimal(tokens[0]!)
  let sign = 1

  for (let index = 1; index < tokens.length; index += 2) {
    const op = tokens[index]
    const value = new Decimal(tokens[index + 1]!)
    if (op === '*') term = term.times(value)
    else if (op === '/') term = term.div(value)
    else {
      total = total.plus(term.times(sign))
      term = value
      sign = op === '-' ? -1 : 1
    }
  }

  total = total.plus(term.times(sign)).toDecimalPlaces(2, Decimal.ROUND_HALF_UP)
  if (!total.isFinite() || total.isZero() || total.isNegative() || total.greaterThan(MaximumAmount)) {
    return null
  }

  return total.toNumber()
}
