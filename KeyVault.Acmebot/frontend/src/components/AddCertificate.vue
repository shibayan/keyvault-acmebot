<script setup lang="ts">
import { ref } from 'vue';
import { CertificatePolicy } from '../types'
import { toUnicode } from '../utils'
import punycode from 'punycode'

const policy = ref({} as CertificatePolicy)

const loading = ref(false)
const sending = ref(false)
const modalActive = ref(true)

const zones = ref([] as string[])
const zoneName = ref("")
const recordName = ref("")
const useAdvancedOptions = ref(false)

const addDnsName = (): void => {
  if (!zoneName.value) {
    return;
  }

  const dnsName = recordName.value ? zoneName.value : punycode.toASCII(recordName.value) + "." + zoneName.value;

  if (policy.value.dnsNames.indexOf(dnsName) === -1) {
    policy.value.dnsNames.push(dnsName);
  }

  recordName.value = "";
}

const removeDnsName = (dnsName: string): void => {
  policy.value.dnsNames = policy.value.dnsNames.filter(x => x !== dnsName);
}

const add = (policy: CertificatePolicy): void => {

}
</script>

<template>
  <div class="modal" :class="{ 'is-active': modalActive }">
    <div class="modal-background"></div>
    <div class="modal-card">
      <header class="modal-card-head">
        <p class="modal-card-title">Add certificate</p>
      </header>
      <section class="modal-card-body">
        <div class="field is-horizontal">
          <div class="field-label is-normal">
            <label class="label">DNS Zone</label>
          </div>
          <div class="field-body">
            <div class="field">
              <div class="control">
                <div class="select" :class="{ 'is-loading': loading }">
                  <select v-model="zoneName">
                    <option disabled value="">Please select one</option>
                    <option v-for="zone in zones" :value="zone">{{ toUnicode(zone) }}</option>
                  </select>
                </div>
              </div>
            </div>
          </div>
        </div>
        <div class="field is-horizontal">
          <div class="field-label is-normal">
            <label class="label">DNS Names</label>
          </div>
          <div class="field-body">
            <div class="field has-addons">
              <p class="control">
                <input v-model="recordName" class="input" type="text" placeholder="Record name"
                  :disabled="zoneName.length === 0">
              </p>
              <p class="control">
                <a class="button is-static">
                  .{{ toUnicode(zoneName) }}
                </a>
              </p>
              <p class="control">
                <button class="button is-info" @click="addDnsName" :disabled="zoneName.length === 0">Add</button>
              </p>
            </div>
          </div>
        </div>
        <div class="field is-horizontal">
          <div class="field-label"></div>
          <div class="field-body">
            <div class="content">
              <div class="tags">
                <span v-for="dnsName in policy.dnsNames" class="tag is-light is-medium">
                  {{ toUnicode(dnsName) }}
                  <button class="delete is-small" @click="removeDnsName(dnsName)"></button>
                </span>
              </div>
            </div>
          </div>
        </div>
        <div class="field is-horizontal">
          <div class="field-label">
            <label class="label">Use Advanced Options?</label>
          </div>
          <div class="field-body">
            <div class="field is-narrow">
              <div class="control">
                <label class="radio">
                  <input type="radio" v-model="useAdvancedOptions" v-bind:value="true">
                  Yes
                </label>
                <label class="radio">
                  <input type="radio" v-model="useAdvancedOptions" v-bind:value="false">
                  No
                </label>
              </div>
            </div>
          </div>
        </div>
        <div class="field is-horizontal" v-if="useAdvancedOptions">
          <div class="field-label is-normal">
            <label class="label">Certificate Name</label>
          </div>
          <div class="field-body">
            <div class="field">
              <p class="control">
                <input v-model="policy.certificateName" class="input" type="text" placeholder="Certificate name">
              </p>
            </div>
          </div>
        </div>
        <div class="field is-horizontal" v-if="useAdvancedOptions">
          <div class="field-label">
            <label class="label">Key Type</label>
          </div>
          <div class="field-body">
            <div class="field">
              <div class="control">
                <label class="radio">
                  <input type="radio" value="RSA" v-model="policy.keyType">
                  RSA
                </label>
                <label class="radio">
                  <input type="radio" value="EC" v-model="policy.keyType">
                  EC
                </label>
              </div>
            </div>
          </div>
        </div>
        <div class="field is-horizontal" v-if="useAdvancedOptions && policy.keyType === 'RSA'">
          <div class="field-label">
            <label class="label">Key Size</label>
          </div>
          <div class="field-body">
            <div class="field">
              <div class="control">
                <label class="radio">
                  <input type="radio" value="2048" v-model="policy.keySize">
                  2048
                </label>
                <label class="radio">
                  <input type="radio" value="3072" v-model="policy.keySize">
                  3072
                </label>
                <label class="radio">
                  <input type="radio" value="4096" v-model="policy.keySize">
                  4096
                </label>
              </div>
            </div>
          </div>
        </div>
        <div class="field is-horizontal" v-if="useAdvancedOptions && policy.keyType === 'EC'">
          <div class="field-label">
            <label class="label">Elliptic Curve Name</label>
          </div>
          <div class="field-body">
            <div class="field">
              <div class="control">
                <label class="radio">
                  <input type="radio" value="P-256" v-model="policy.keyCurveName">
                  P-256
                </label>
                <label class="radio">
                  <input type="radio" value="P-384" v-model="policy.keyCurveName">
                  P-384
                </label>
                <label class="radio">
                  <input type="radio" value="P-521" v-model="policy.keyCurveName">
                  P-521
                </label>
                <label class="radio">
                  <input type="radio" value="P-256K" v-model="policy.keyCurveName">
                  P-256K
                </label>
              </div>
            </div>
          </div>
        </div>
        <div class="field is-horizontal" v-if="useAdvancedOptions">
          <div class="field-label">
            <label class="label">Reuse Key on Renewal?</label>
          </div>
          <div class="field-body">
            <div class="field">
              <div class="control">
                <label class="radio">
                  <input type="radio" v-model="policy.reuseKey" v-bind:value="true">
                  Yes
                </label>
                <label class="radio">
                  <input type="radio" v-model="policy.reuseKey" v-bind:value="false">
                  No
                </label>
              </div>
            </div>
          </div>
        </div>
      </section>
      <footer class="modal-card-foot is-justify-content-flex-end">
        <button class="button is-primary" @click="add(policy)" :class="{ 'is-loading': sending }">Add</button>
        <button class="button" @click="modalActive = false" :disabled="sending">Cancel</button>
      </footer>
    </div>
  </div>
</template>
