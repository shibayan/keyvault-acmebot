export interface Certificate {
  createdOn: string
  dnsNames: string[]
  expiresOn: string
  id: string
  isExpired: boolean
  isManaged: boolean
  keyCurveName?: string
  keySize?: number
  keyType: string
  name: string
  reuseKey: boolean
  x509Thumbprint: string
}

export interface CertificatePolicy {
  certificateName: string
  dnsNames: string[]
  keyType: string
  keySize?: number
  keyCurveName?: string
  reuseKey?: boolean
}
