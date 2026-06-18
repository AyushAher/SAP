declare module 'jsencrypt' {
  export default class JSEncrypt {
    constructor()
    setPublicKey(key: string): void
    setPrivateKey(key: string): void
    encrypt(data: string): string | false
    decrypt(data: string): string | false
  }
}
