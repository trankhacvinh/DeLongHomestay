const {test} = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const source = fs.readFileSync('src/DeLong.Web/wwwroot/js/pages/admin-customer-autocomplete.js', 'utf8');

async function lookup(customer) {
    let configuration;
    const input = {isConnected:true};
    const owner = {propertyId:'p',editor:{mode:'create'},form:{customerPhone:customer.phone,customerName:'',customerEmail:'',customerId:null},
        $el:{nodeType:1,querySelector:()=>input}};
    class TomSelect {
        constructor(element, config) {configuration=config;this.options={};}
        addOption(item) {this.options[item.phone]=item;}
        updateOption(phone,item) {this.options[phone]=item;}
        setValue(phone) {configuration.onChange.call(this,phone);}
    }
    const scope = {window:{TomSelect,Vue:{markRaw:value=>value}},TomSelect,DeLongApi:{get:async()=>[customer]}};
    vm.runInNewContext(source,scope);
    scope.window.DeLongCustomerAutocomplete.methods.attachCustomerAutocomplete.call(owner);
    let loaded;
    await configuration.load.call(owner.customerAutocomplete,customer.phone,options=>{loaded=options;});
    return {owner,loaded};
}

test('hidden customer remains selectable and exact phone fills the same existing profile',async()=>{
    const {owner,loaded}=await lookup({id:'existing',phone:'0900000123',name:'Synthetic',email:'guest@example.test',isActive:false,isBlocked:false});
    assert.equal(loaded[0].disabled,false);
    assert.equal(owner.form.customerId,'existing');
    assert.equal(owner.form.customerName,'Synthetic');
    assert.equal(owner.form.customerEmail,'guest@example.test');
});
test('manual block stays disabled even for hidden profiles',async()=>{
    const {owner,loaded}=await lookup({id:'blocked',phone:'0900000123',name:'Synthetic',isActive:false,isBlocked:true});
    assert.equal(loaded[0].disabled,true);
    assert.equal(owner.form.customerId,null);
});
